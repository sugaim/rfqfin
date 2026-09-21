using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class QuoteConfirmationWorkflowTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OwnerConfirmSnapshotsWithoutCalculationAndRecordsTransition()
    {
        var state = new State();
        var useCase = new ConfirmQuote(
            state.Cases,
            state.WorkingQuotes,
            state.ConfirmedQuotes,
            new RfqAuthorization(),
            Current("trader-a", UserRole.Trader),
            state.Events,
            state.UnitOfWork,
            new FixedTimeProvider());

        var result = await useCase.ExecuteAsync(
            state.Case.CaseId.Value,
            15,
            state.Case.CurrentVersion,
            state.WorkingQuote.Version);

        var snapshot = Assert.Single(state.ConfirmedQuotes.Items);
        Assert.Equal(99.5m, snapshot.Calculated?.Price);
        Assert.Equal(Now.AddMinutes(15), snapshot.ExpiresAt);
        Assert.Equal("Quoted", result.QuoteStatus);
        Assert.Equal(QuoteTransitionKind.Confirmed, Assert.Single(state.Events.Items).Kind);
        Assert.Equal(1, state.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task OnlyOwningTraderCanConfirm()
    {
        var state = new State();
        var useCase = new ConfirmQuote(
            state.Cases,
            state.WorkingQuotes,
            state.ConfirmedQuotes,
            new RfqAuthorization(),
            Current("trader-b", UserRole.Trader),
            state.Events,
            state.UnitOfWork,
            new FixedTimeProvider());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(
            state.Case.CaseId.Value,
            null,
            state.Case.CurrentVersion,
            state.WorkingQuote.Version));
        Assert.Empty(state.ConfirmedQuotes.Items);
    }

    [Fact]
    public async Task OnlyContactOwnerCanPresentAndUnpresent()
    {
        var state = new State();
        state.Case.ConfirmQuote(
            QuoteId.New(),
            state.Case.InitialRevision.RevisionId,
            state.Case.CurrentVersion);
        var forbidden = new PresentQuote(
            state.Cases,
            new RfqAuthorization(),
            Current("sales-a", UserRole.Sales),
            state.Events,
            state.UnitOfWork,
            new FixedTimeProvider());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => forbidden.ExecuteAsync(
            state.Case.CaseId.Value,
            state.Case.CurrentVersion));

        var owner = new PresentQuote(
            state.Cases,
            new RfqAuthorization(),
            Current("sales-dev", UserRole.Sales),
            state.Events,
            state.UnitOfWork,
            new FixedTimeProvider());
        var presented = await owner.ExecuteAsync(
            state.Case.CaseId.Value,
            state.Case.CurrentVersion);
        Assert.Equal("Presented", presented.RfqStatus);

        var unpresent = new UnpresentQuote(
            state.Cases,
            new RfqAuthorization(),
            Current("sales-dev", UserRole.Sales),
            state.Events,
            state.UnitOfWork,
            new FixedTimeProvider());
        var active = await unpresent.ExecuteAsync(
            state.Case.CaseId.Value,
            state.Case.CurrentVersion);
        Assert.Equal("Active", active.RfqStatus);
    }

    private sealed class State
    {
        public State()
        {
            Case = CreateOwnedCase();
            WorkingQuote = WorkingQuote.CreateEmpty(
                Case.InitialRevision.RevisionId,
                UserId.Create("trader-a"),
                Now);
            WorkingQuote.ApplyCalculated(new CalculatedQuotePayload(
                CalculationDriver.Price,
                99.5m,
                99.5m,
                0.8m,
                0.81m,
                0.03m,
                0.84m,
                0.83m,
                5m,
                8m), WorkingQuote.Version, UserId.Create("trader-a"), Now);
            Cases = new StubRfqRepository(Case);
            WorkingQuotes = new StubWorkingQuoteRepository(WorkingQuote);
        }

        public RfqCase Case { get; }
        public WorkingQuote WorkingQuote { get; }
        public StubRfqRepository Cases { get; }
        public StubWorkingQuoteRepository WorkingQuotes { get; }
        public RecordingConfirmedQuoteRepository ConfirmedQuotes { get; } = new();
        public RecordingEventSink Events { get; } = new();
        public RecordingUnitOfWork UnitOfWork { get; } = new();
    }

    private static RfqCase CreateOwnedCase()
    {
        var sales = UserId.Create("sales-dev");
        var rfqCase = RfqCase.CreateDraft(
            new CaseId(20),
            ClientId.Create("client-1"),
            SecurityId.Create("security-1"),
            CategoryId.Create("JGB"),
            UserId.Create("trader-a"),
            100_000_000m,
            new DateOnly(2026, 9, 24),
            new DateOnly(2026, 9, 23),
            null,
            sales,
            Now);
        rfqCase.ConfirmInitial(new DateOnly(2026, 9, 21), sales, Now, 1);
        rfqCase.PickUp(UserId.Create("trader-a"), rfqCase.CurrentVersion);
        return rfqCase;
    }

    private static ICurrentUser Current(string id, UserRole role) =>
        new StubCurrentUser(new CurrentUser(
            UserId.Create(id),
            new HashSet<UserRole> { role },
            "jpy-credit"));

    private sealed class StubCurrentUser(CurrentUser user) : ICurrentUser
    {
        public CurrentUser User { get; } = user;
    }

    private sealed class StubRfqRepository(RfqCase rfqCase) : IRfqCaseRepository
    {
        public void Add(RfqCase item) => throw new NotSupportedException();
        public Task<RfqCase?> GetAsync(CaseId caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RfqCase?>(rfqCase);
        public void Update(RfqCase item) { }
        public Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(UserId user, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalesRfqListItem>>([]);
        public Task<IReadOnlyList<TraderRfqListItem>> GetActiveTraderRfqsAsync(string deskId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TraderRfqListItem>>([]);
    }

    private sealed class StubWorkingQuoteRepository(WorkingQuote quote) : IWorkingQuoteRepository
    {
        public Task<QuoteEditContext?> GetEditContextAsync(CaseId caseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<WorkingQuote?> GetAsync(RevisionId revisionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkingQuote?>(quote);
        public void Update(WorkingQuote workingQuote) => throw new NotSupportedException();
        public void AddFailure(CalculationFailureRecord failure) => throw new NotSupportedException();
    }

    private sealed class RecordingConfirmedQuoteRepository : IConfirmedQuoteRepository
    {
        public List<ConfirmedQuote> Items { get; } = [];
        public void Add(ConfirmedQuote quote) => Items.Add(quote);
        public Task<ConfirmedQuote?> GetAsync(QuoteId quoteId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.QuoteId == quoteId));
    }

    private sealed class RecordingEventSink : IQuoteEventSink
    {
        public List<QuoteTransition> Items { get; } = [];
        public void Record(QuoteTransition transition) => Items.Add(transition);
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
