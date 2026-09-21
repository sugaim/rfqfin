using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class SemanticApplicationTests
{
    private static readonly DateOnly Today = new(2026, 9, 21);
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 2, 0, 0, TimeSpan.Zero);
    private static readonly UserId Sales = UserId.Create("sales");
    private static readonly UserId Trader = UserId.Create("trader");

    [Fact]
    public async Task Initial_confirm_creates_working_quote_and_commits_once()
    {
        var draft = Draft();
        var cases = new CaseRepository(draft);
        var working = new WorkingRepository();
        var uow = new UnitOfWork();
        var useCase = new ConfirmInitialDraft(
            cases, new AssignedTraderValidator(new Users(), Current(Sales, UserRole.Sales)),
            working, new BusinessDate(), new RfqAuthorization(),
            Current(Sales, UserRole.Sales), uow, TimeProvider.System, new RfqEvents());

        await useCase.ExecuteAsync(new UpdateInitialDraftCommand(
            draft.CaseId, 1_000_000, Today.AddDays(2), Today.AddDays(2), "confirmed",
            Trader, draft.CurrentRevision.Version));

        Assert.Single(working.Added);
        Assert.Equal(cases.Case!.CurrentRevision.RevisionId, working.Added[0].RevisionId);
        Assert.Equal(1, uow.Saves);
    }

    [Fact]
    public async Task Confirm_new_creates_working_quote_in_the_same_commit()
    {
        var current = Current(Sales, UserRole.Sales);
        var cases = new CaseRepository(null);
        var working = new WorkingRepository();
        var uow = new UnitOfWork();
        var useCase = new ConfirmNewRfq(
            Factory(current), cases, working, new BusinessDate(), new RfqAuthorization(),
            current, uow, TimeProvider.System, new RfqEvents());

        var result = await useCase.ExecuteAsync(Command());

        Assert.Single(working.Added);
        Assert.Equal(result.RevisionId, working.Added[0].RevisionId);
        Assert.Equal(result.RevisionId, cases.Added!.CurrentRevision.RevisionId);
        Assert.Equal(1, uow.Saves);
    }

    [Fact]
    public async Task Sales_created_rfq_records_sales_id()
    {
        var current = Current(Sales, UserRole.Sales);

        var rfq = await Factory(current).CreateAsync(Command());

        Assert.Equal(Sales, rfq.SalesId);
        Assert.Equal(Sales, rfq.ContactOwnerId);
    }

    [Fact]
    public async Task Trader_created_rfq_has_no_sales_id()
    {
        var current = Current(Trader, UserRole.Trader);

        var rfq = await Factory(current).CreateAsync(Command());

        Assert.Null(rfq.SalesId);
        Assert.Equal(Trader, rfq.ContactOwnerId);
    }

    [Fact]
    public async Task Creation_context_requires_category_routing()
    {
        var current = Current(Sales, UserRole.Sales);
        var resolver = new ResolveRfqCreationContext(
            new Securities(), new MissingRouting(), new Users(),
            new BusinessDate(), new Settlement(), current);

        await Assert.ThrowsAsync<RfqInvariantException>(() =>
            resolver.ExecuteAsync(SecurityId.Create("security")));
    }

    [Fact]
    public async Task Request_validation_missing_resource_and_forbidden_are_semantically_typed()
    {
        var validator = new AssignedTraderValidator(
            new Users(), Current(Sales, UserRole.Sales));
        await Assert.ThrowsAsync<RfqRequestValidationException>(() =>
            validator.ResolveAsync(Sales));

        var missing = new UpdateInitialDraft(
            new CaseRepository(null), validator, new RfqAuthorization(),
            Current(Sales, UserRole.Sales), new UnitOfWork());
        await Assert.ThrowsAsync<RfqNotFoundException>(() => missing.ExecuteAsync(
            new UpdateInitialDraftCommand(new CaseId(999), 1_000_000,
                Today, Today, "", Trader, new StateVersion(1))));

        var draft = Draft();
        var forbidden = new UpdateInitialDraft(
            new CaseRepository(draft), validator, new RfqAuthorization(),
            Current(Trader, UserRole.Trader), new UnitOfWork());
        await Assert.ThrowsAsync<RfqForbiddenException>(() => forbidden.ExecuteAsync(
            new UpdateInitialDraftCommand(draft.CaseId, 1_000_000,
                Today, Today, "", Trader, draft.Version)));
    }

    [Fact]
    public async Task Trader_screen_query_does_not_write()
    {
        var cases = new CaseRepository(null);
        var queries = new TraderRfqQueries([]);
        var useCase = new GetActiveTraderRfqs(
            queries, new RfqAuthorization(), Current(Trader, UserRole.Trader));

        var result = await useCase.ExecuteAsync();

        Assert.Empty(result);
        Assert.Equal(1, queries.TraderQueries);
        Assert.Equal(0, cases.Updates);
    }

    [Fact]
    public async Task Quote_confirmation_allocates_identity_before_persistence_and_commits_atomically()
    {
        var rfq = RfqOwnershipTransitions.PickUp(Open(), Trader, Open().Version);
        var quote = WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now);
        quote = WorkingQuoteTransitions.ApplyCalculated(
            quote, Payload(), quote.Version, Trader, Now);
        var cases = new CaseRepository(rfq);
        var working = new WorkingRepository(quote);
        var confirmed = new ConfirmedRepository();
        var uow = new UnitOfWork();
        var useCase = new ConfirmQuote(
            cases, working, confirmed, new RfqAuthorization(),
            Current(Trader, UserRole.Trader), new QuoteEvents(), uow, TimeProvider.System);

        var result = await useCase.ExecuteAsync(
            rfq.CaseId, new QuoteExpiry.After(TimeSpan.FromMinutes(5)), rfq.Version, quote.Version);

        Assert.NotEqual(Guid.Empty, result.QuoteId.Value);
        Assert.Equal(result.QuoteId, Assert.Single(confirmed.Added).QuoteId);
        Assert.Equal(result.QuoteId, cases.Case!.CurrentQuoteId!.Value);
        Assert.Equal(1, uow.Saves);
    }

    [Fact]
    public async Task Amendment_confirm_seeds_new_working_quote_in_same_commit()
    {
        var rfq = Open();
        var seed = WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now);
        seed = WorkingQuoteTransitions.ApplyCalculated(seed, Payload(), seed.Version, Trader, Now);
        var saved = AmendmentTransitions.SaveDraft(
            rfq, RevisionId.New(), new RevisionTerms(2_000_000, Today.AddDays(3),
                Today.AddDays(2), "amended"), Sales, Now, rfq.Version);
        var cases = new CaseRepository(saved.Rfq);
        var working = new WorkingRepository(seed);
        var uow = new UnitOfWork();
        var useCase = new ConfirmAmendment(
            cases, working, new BusinessDate(), new RfqAuthorization(),
            Current(Sales, UserRole.Sales), new RfqEvents(), uow, TimeProvider.System);

        await useCase.ExecuteAsync(new AmendmentItem(
            saved.Rfq.CaseId, saved.Rfq.Version,
            saved.DraftRevision.Version));

        var added = Assert.Single(working.Added);
        Assert.Equal(cases.Case!.CurrentRevision.RevisionId, added.RevisionId);
        Assert.Equal(seed.Calculated, added.Calculated);
        Assert.Single(cases.ChangedRevisions);
        Assert.Equal(1, uow.Saves);
    }

    [Fact]
    public async Task Calculation_revalidates_case_after_external_call()
    {
        var rfq = RfqOwnershipTransitions.PickUp(Open(), Trader, Open().Version);
        var quote = WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now);
        var cases = new CaseRepository(rfq);
        var working = new WorkingRepository(quote, rfq);
        var calculation = new CalculationClient(_ =>
        {
            cases.Case = RfqResponsibilityTransitions.ChangeContactOwner(
                cases.Case!, UserId.Create("other"), cases.Case!.Version);
            return new CalculationSuccess(Guid.Empty, Payload());
        });
        var useCase = new CalculateWorkingQuote(
            cases, working, calculation, new RfqAuthorization(),
            Current(Trader, UserRole.Trader), new UnitOfWork(), TimeProvider.System);

        await Assert.ThrowsAsync<StateVersionMismatchException>(() => useCase.ExecuteAsync(
            rfq.CaseId, CalculationDriver.Price, 100m, 0m,
            rfq.Version, quote.Version));
        Assert.Equal(0, working.Updates);
    }

    [Fact]
    public async Task Calculation_failure_records_log_without_mutating_working_quote()
    {
        var rfq = RfqOwnershipTransitions.PickUp(Open(), Trader, Open().Version);
        var quote = WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now);
        var working = new WorkingRepository(quote, rfq);
        var useCase = new CalculateWorkingQuote(
            new CaseRepository(rfq), working,
            new CalculationClient(_ => new CalculationError(Guid.Empty, "BAD", "failed")),
            new RfqAuthorization(), Current(Trader, UserRole.Trader),
            new UnitOfWork(), TimeProvider.System);

        await Assert.ThrowsAsync<CalculationFailureException>(() => useCase.ExecuteAsync(
            rfq.CaseId, CalculationDriver.Price, 100m, 0m,
            rfq.Version, quote.Version));
        Assert.Equal(0, working.Updates);
        Assert.Single(working.Failures);
    }

    [Fact]
    public async Task Create_from_existing_uses_desk_business_date_not_utc_calendar_date()
    {
        var createdAt = new DateTimeOffset(2026, 9, 20, 15, 30, 0, TimeSpan.Zero);
        var source = Open(createdAt);
        var cases = new CaseRepository(source);
        var current = Current(Sales, UserRole.Sales);
        var factory = Factory(current);
        var deskLocalDates = new DeskLocalDates(Today);
        var useCase = new CreateFromExisting(
            cases, factory, new ResolveRfqCreationContext(
                new Securities(), new Routing(), new Users(),
                new BusinessDate(), new Settlement(), current),
            new BusinessDate(), deskLocalDates,
            new RfqAuthorization(), current, new UnitOfWork());

        await useCase.ExecuteAsync(source.CaseId);

        Assert.Equal(createdAt, deskLocalDates.ReceivedInstant);
        Assert.Equal(source.CurrentRevision.SettlementDate, cases.Added!.CurrentRevision.SettlementDate);
    }

    [Fact]
    public async Task Bulk_withdraw_skips_an_already_requested_quote()
    {
        var rfq = Open();
        rfq = RfqOwnershipTransitions.PickUp(rfq, Trader, rfq.Version);
        var unitOfWork = new UnitOfWork();
        var single = new WithdrawQuote(new CaseRepository(rfq), new RfqAuthorization(),
            Current(Trader, UserRole.Trader), new QuoteEvents(), unitOfWork,
            TimeProvider.System);
        var bulk = new BulkWithdrawQuotes(single, unitOfWork);

        var result = Assert.Single(await bulk.ExecuteAsync(
            [new LifecycleItem(rfq.CaseId, rfq.Version)]));

        Assert.Equal(BulkItemStatus.Skipped, result.Status);
        Assert.Equal(0, unitOfWork.Saves);
    }

    [Fact]
    public async Task Bulk_close_away_skips_away_but_fails_hit()
    {
        var (quoted, _) = Quoted();
        var away = RfqLifecycleTransitions.CloseAway(quoted, quoted.Version).Rfq;
        var awayUnit = new UnitOfWork();
        var awayBulk = new BulkCloseAwayRfqs(new CloseAwayRfq(
            new CaseRepository(away), new RfqAuthorization(), Current(Sales, UserRole.Sales),
            new RfqEvents(), awayUnit, TimeProvider.System), awayUnit);
        var skipped = Assert.Single(await awayBulk.ExecuteAsync(
            [new LifecycleItem(away.CaseId, away.Version)]));
        Assert.Equal(BulkItemStatus.Skipped, skipped.Status);

        var hit = RfqLifecycleTransitions.CloseHit(quoted, quoted.Version).Rfq;
        var hitUnit = new UnitOfWork();
        var hitBulk = new BulkCloseAwayRfqs(new CloseAwayRfq(
            new CaseRepository(hit), new RfqAuthorization(), Current(Sales, UserRole.Sales),
            new RfqEvents(), hitUnit, TimeProvider.System), hitUnit);
        var failed = Assert.Single(await hitBulk.ExecuteAsync(
            [new LifecycleItem(hit.CaseId, hit.Version)]));
        Assert.Equal(BulkItemStatus.Failed, failed.Status);
        Assert.Equal(BulkFailureCode.InvalidState, failed.Code);
        Assert.Equal(1, hitUnit.Discards);
    }

    private static InitialRfqFactory Factory(ICurrentUser current) => new(
        new CaseIds(), new Clients(),
        new ResolveRfqCreationContext(new Securities(), new Routing(), new Users(),
            new BusinessDate(), new Settlement(), current),
        new AssignedTraderValidator(new Users(), current), current, TimeProvider.System);

    private static CreateDraftCommand Command() => new(
        ClientId.Create("client"), SecurityId.Create("security"), 1_000_000,
        Today.AddDays(2), Today.AddDays(2), "message", Trader);

    private static RfqCase Draft(DateTimeOffset? createdAt = null) => RfqCase.CreateDraft(
        new CaseId(10), RevisionId.New(), ClientId.Create("client"), SecurityId.Create("security"),
        CategoryId.Create("category"), Trader,
        new RevisionTerms(1_000_000, Today.AddDays(2), Today.AddDays(2), ""),
        Sales, createdAt ?? Now);

    private static RfqCase Open(DateTimeOffset? createdAt = null)
    {
        var draft = Draft(createdAt);
        return RfqLifecycleTransitions.ConfirmInitial(
            draft, draft.CurrentRevision.Terms, Trader, Today, Sales, Now,
            draft.CurrentRevision.Version);
    }

    private static CalculatedQuotePayload Payload() => new(
        CalculationDriver.Price, 100m, 100m, 1m, 1m, 0m, 1m, 1m, 10m, 10m);

    private static (RfqCase Rfq, ConfirmedQuote Quote) Quoted()
    {
        var rfq = Open();
        var working = WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now);
        working = WorkingQuoteTransitions.ApplyCalculated(
            working, Payload(), working.Version, Trader, Now);
        var result = QuoteTransitions.Confirm(rfq, working, QuoteId.New(),
            new QuoteConfirmation(Trader, Now, new QuoteExpiry.None()));
        return (result.Rfq, result.ConfirmedQuote);
    }

    private static ICurrentUser Current(UserId id, UserRole role) =>
        new CurrentUserService(new CurrentUser(
            id, new HashSet<UserRole> { role }, DeskId.Create("desk")));

    private sealed record CurrentUserService(CurrentUser User) : ICurrentUser;

    private sealed class CaseRepository(RfqCase? value) : IRfqCaseRepository
    {
        public RfqCase? Case { get; set; } = value;
        public RfqCase? Added { get; private set; }
        public List<RfqRevision> ChangedRevisions { get; } = [];
        public int Updates { get; private set; }
        public void Add(RfqCase rfq) { Added = rfq; Case = rfq; }
        public Task<RfqCase?> GetAsync(CaseId id, CancellationToken token = default) =>
            Task.FromResult(Case?.CaseId == id ? Case : null);
        public void Update(RfqCase rfq) { Updates++; Case = rfq; }
        public void UpdateRevision(RfqRevision revision) => ChangedRevisions.Add(revision);
    }

    private sealed class TraderRfqQueries(
        IReadOnlyList<TraderRfqListItem> traderRows) : ITraderRfqQueries
    {
        public int TraderQueries { get; private set; }

        public Task<IReadOnlyList<TraderRfqListItem>> GetAsync(
            DeskId desk,
            CancellationToken token = default)
        {
            TraderQueries++;
            return Task.FromResult(traderRows);
        }
    }

    private sealed class WorkingRepository : IWorkingQuoteRepository
    {
        private readonly Dictionary<RevisionId, WorkingQuote> quotes = [];
        private readonly RfqCase? contextCase;
        public WorkingRepository(WorkingQuote? quote = null, RfqCase? contextCase = null)
        {
            if (quote is not null) quotes[quote.RevisionId] = quote;
            this.contextCase = contextCase;
        }
        public List<WorkingQuote> Added { get; } = [];
        public List<CalculationFailureRecord> Failures { get; } = [];
        public int Updates { get; private set; }
        public void Add(WorkingQuote quote) { Added.Add(quote); quotes[quote.RevisionId] = quote; }
        public Task<WorkingQuote?> GetAsync(RevisionId id, CancellationToken token = default) =>
            Task.FromResult(quotes.GetValueOrDefault(id));
        public Task<QuoteEditContext?> GetEditContextAsync(CaseId id, CancellationToken token = default)
        {
            var rfq = contextCase!;
            var quote = quotes[rfq.CurrentRevision.RevisionId];
            return Task.FromResult<QuoteEditContext?>(new(
                rfq.CaseId, rfq.CurrentRevision.RevisionId, rfq.SecurityId,
                rfq.CurrentRevision.SettlementDate!.Value, rfq.Version,
                rfq.AssignedTraderId, rfq.Ownership!,
                ((ActiveRfq)rfq.Lifecycle).QuoteState, quote));
        }
        public void Update(WorkingQuote quote) { Updates++; quotes[quote.RevisionId] = quote; }
        public void AddFailure(CalculationFailureRecord failure) => Failures.Add(failure);
    }

    private sealed class ConfirmedRepository : IConfirmedQuoteRepository
    {
        public List<ConfirmedQuote> Added { get; } = [];
        public void Add(ConfirmedQuote quote) => Added.Add(quote);
        public Task<ConfirmedQuote?> GetAsync(QuoteId id, CancellationToken token = default) =>
            Task.FromResult(Added.SingleOrDefault(x => x.QuoteId == id));
    }

    private sealed class CalculationClient(Func<CalculationRequest, CalculationResult> result)
        : ICalculationClient
    {
        public Task<IReadOnlyList<CalculationResult>> CalculateBulkAsync(
            IReadOnlyList<CalculationRequest> requests, CancellationToken token = default)
        {
            var value = result(requests.Single());
            value = value switch
            {
                CalculationSuccess success => success with { RequestId = requests[0].RequestId },
                CalculationError error => error with { RequestId = requests[0].RequestId },
                _ => value,
            };
            return Task.FromResult<IReadOnlyList<CalculationResult>>([value]);
        }
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public int Discards { get; private set; }
        public Task SaveChangesAsync(CancellationToken token = default) { Saves++; return Task.CompletedTask; }
        public void DiscardChanges() { Discards++; }
    }
    private sealed class QuoteEvents : IQuoteEventSink { public void Record(QuoteTransition transition) { } }
    private sealed class RfqEvents : IRfqEventSink { public void Record(RfqTransition transition) { } }
    private sealed class BusinessDate : IBusinessDateProvider { public Task<DateOnly> GetCurrentAsync(CancellationToken token = default) => Task.FromResult(Today); }
    private sealed class DeskLocalDates(DateOnly date) : IDeskLocalDateResolver
    {
        public DateTimeOffset? ReceivedInstant { get; private set; }
        public Task<DateOnly> ResolveAsync(
            DateTimeOffset instant,
            DeskId desk,
            CancellationToken token = default)
        { ReceivedInstant = instant; return Task.FromResult(date); }
    }
    private sealed class CaseIds : ICaseIdGenerator { public Task<CaseId> NextAsync(CancellationToken token = default) => Task.FromResult(new CaseId(11)); }
    private sealed class Clients : IClientSearch
    {
        public Task<IReadOnlyList<ClientSearchResult>> SearchAsync(string q, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ClientSearchResult?> ResolveAsync(ClientId id, CancellationToken token = default) => Task.FromResult<ClientSearchResult?>(new(id, id.Value, "Client"));
    }
    private sealed class Securities : ISecuritySearch
    {
        public Task<IReadOnlyList<SecuritySearchResult>> SearchAsync(string q, CancellationToken token = default) => throw new NotSupportedException();
        public Task<SecuritySearchResult?> ResolveAsync(SecurityId id, CancellationToken token = default) => Task.FromResult<SecuritySearchResult?>(new(id, "Security", "SEC", "1-01-0001-00001", "ISIN", CategoryId.Create("category"), "Category"));
    }
    private sealed class Routing : ICategoryRouting
    {
        public Task<UserId> GetDefaultAssignedTraderAsync(CategoryId id, CancellationToken token = default) => Task.FromResult(Trader);
        public Task<IReadOnlyList<CategoryRoutingItem>> GetAllAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<CategoryRoutingItem> SetDefaultAssignedTraderAsync(CategoryId id, UserId traderId, CancellationToken token = default) => throw new NotSupportedException();
    }
    private sealed class MissingRouting : ICategoryRouting
    {
        public Task<UserId> GetDefaultAssignedTraderAsync(CategoryId id, CancellationToken token = default) =>
            throw new RfqInvariantException("Category routing is missing.");
        public Task<IReadOnlyList<CategoryRoutingItem>> GetAllAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<CategoryRoutingItem> SetDefaultAssignedTraderAsync(CategoryId id, UserId traderId, CancellationToken token = default) => throw new NotSupportedException();
    }
    private sealed class Settlement : IStandardSettlementResolver { public DateOnly Resolve(SecurityId id, DateOnly date) => date.AddDays(2); }
    private sealed class Users : IUserDirectory
    {
        public Task<IReadOnlyList<UserSummary>> GetUsersAsync(UserRole? role = null, CancellationToken token = default) => throw new NotSupportedException();
        public Task<UserSummary?> ResolveAsync(UserId id, CancellationToken token = default) => Task.FromResult<UserSummary?>(
            new(id, id.Value, new HashSet<UserRole> { id == Trader ? UserRole.Trader : UserRole.Sales },
                DeskId.Create("desk")));
    }
}
