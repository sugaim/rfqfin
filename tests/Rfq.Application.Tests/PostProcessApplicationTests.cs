using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class PostProcessApplicationTests
{
    private static readonly DateOnly Today = new(2026, 9, 21);
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 2, 0, 0, TimeSpan.Zero);
    private static readonly UserId Sales = UserId.Create("sales");
    private static readonly UserId Trader = UserId.Create("trader");

    [Fact]
    public async Task Close_and_cancel_record_the_current_business_date()
    {
        RfqCase quoted = Quoted(new CaseId(1));
        var closeCases = new Cases(quoted);
        var closeEvents = new Events();
        var close = new CloseRfqOperation(
            closeCases,
            new RfqAuthorization(),
            Current(Sales, UserRole.Sales),
            closeEvents,
            new BusinessDate(Today),
            TimeProvider.System);

        await close.ApplyAsync(
            quoted.CaseId,
            RfqStatus.Hit,
            quoted.Version,
            default);

        Assert.Equal(Today, closeCases.Get(quoted.CaseId).ClosedBusinessDate);
        Assert.Equal(Today, Assert.Single(closeEvents.Recorded).BusinessDate);

        RfqCase open = Open(new CaseId(2));
        var cancelCases = new Cases(open);
        var cancelEvents = new Events();
        var cancel = new CancelRfqOperation(
            cancelCases,
            new RfqAuthorization(),
            Current(Sales, UserRole.Sales),
            cancelEvents,
            new BusinessDate(Today),
            TimeProvider.System);

        await cancel.ApplyAsync(open.CaseId, open.Version, default);

        RfqTransition cancelled = Assert.Single(cancelEvents.Recorded);
        Assert.Equal(RfqTransitionKind.Cancelled, cancelled.Kind);
        Assert.Equal(Today, cancelled.BusinessDate);
    }

    [Fact]
    public async Task Correction_requires_reason_same_business_date_and_contact_owner()
    {
        RfqCase quoted = Quoted(new CaseId(1));
        RfqCase hit = RfqLifecycleTransitions.CloseHit(
            quoted,
            Today,
            quoted.Version).Rfq;
        var cases = new Cases(hit);
        var events = new Events();
        var sameDay = new CorrectOutcomeOperation(
            cases,
            new RfqAuthorization(),
            Current(Sales, UserRole.Sales),
            events,
            new BusinessDate(Today),
            TimeProvider.System);

        await sameDay.ApplyAsync(
            hit.CaseId,
            "  booking correction  ",
            hit.Version,
            RfqStatus.Away,
            default);

        RfqTransition corrected = Assert.Single(events.Recorded);
        Assert.Equal("booking correction", corrected.Reason);
        Assert.Equal(Today, corrected.BusinessDate);
        Assert.Equal(Today, cases.Get(hit.CaseId).ClosedBusinessDate);

        RfqCase anotherHit = RfqLifecycleTransitions.CloseHit(
            quoted,
            Today,
            quoted.Version).Rfq;
        var anotherCases = new Cases(anotherHit);
        var nextDay = new CorrectOutcomeOperation(
            anotherCases,
            new RfqAuthorization(),
            Current(Sales, UserRole.Sales),
            new Events(),
            new BusinessDate(Today.AddDays(1)),
            TimeProvider.System);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() =>
            nextDay.ApplyAsync(
                anotherHit.CaseId,
                "late",
                anotherHit.Version,
                RfqStatus.Away,
                default));
        await Assert.ThrowsAsync<RfqRequestValidationException>(() =>
            sameDay.ApplyAsync(
                hit.CaseId,
                "  ",
                hit.Version,
                RfqStatus.Away,
                default));

        var wrongActor = new CorrectOutcomeOperation(
            new Cases(anotherHit),
            new RfqAuthorization(),
            Current(Trader, UserRole.Trader),
            new Events(),
            new BusinessDate(Today),
            TimeProvider.System);
        await Assert.ThrowsAsync<RfqForbiddenException>(() =>
            wrongActor.ApplyAsync(
                anotherHit.CaseId,
                "not owner",
                anotherHit.Version,
                RfqStatus.Away,
                default));
    }

    [Fact]
    public async Task Combined_case_commit_saves_once_and_memo_conflict_rolls_back_case()
    {
        RfqCase quoted = Quoted(new CaseId(1));
        var cases = new Cases(quoted);
        var memos = new Memos(quoted.CaseId);
        var unitOfWork = new UnitOfWork();
        CommitPostProcessChanges commit = Committer(cases, memos, unitOfWork);

        IReadOnlyList<BulkItemResult> successResults = await commit.ExecuteAsync(
        [
            new PostProcessCommitItem(
                quoted.CaseId,
                quoted.Version,
                new PostProcessLifecycleChange(PostProcessLifecycleChangeKind.Away),
                new PostProcessMemoChange(new StateVersion(1), "follow tomorrow")),
        ]);
        BulkItemResult succeeded = Assert.Single(successResults);

        Assert.Equal(BulkItemStatus.Succeeded, succeeded.Status);
        Assert.Equal(1, unitOfWork.Saves);
        Assert.Equal(1, memos.SalesUpdates);

        RfqCase second = Quoted(new CaseId(2));
        var conflictCases = new Cases(second);
        var conflictMemos = new Memos(second.CaseId);
        var conflictUnit = new UnitOfWork();
        CommitPostProcessChanges conflicting = Committer(
            conflictCases,
            conflictMemos,
            conflictUnit);
        IReadOnlyList<BulkItemResult> failureResults = await conflicting.ExecuteAsync(
        [
            new PostProcessCommitItem(
                second.CaseId,
                second.Version,
                new PostProcessLifecycleChange(PostProcessLifecycleChangeKind.Hit),
                new PostProcessMemoChange(new StateVersion(99), "conflict")),
        ]);
        BulkItemResult failed = Assert.Single(failureResults);

        Assert.Equal(BulkItemStatus.Failed, failed.Status);
        Assert.Equal(BulkFailureCode.VersionConflict, failed.Code);
        Assert.Equal(0, conflictUnit.Saves);
        Assert.Equal(1, conflictUnit.Discards);
    }

    [Fact]
    public async Task Case_failure_is_discarded_and_does_not_block_the_next_case()
    {
        RfqCase first = Quoted(new CaseId(1));
        RfqCase second = Quoted(new CaseId(2));
        var cases = new Cases(first, second);
        var memos = new Memos(first.CaseId, second.CaseId);
        var unitOfWork = new UnitOfWork();
        CommitPostProcessChanges commit = Committer(cases, memos, unitOfWork);

        IReadOnlyList<BulkItemResult> results = await commit.ExecuteAsync(
        [
            new PostProcessCommitItem(
                first.CaseId,
                new StateVersion(999),
                new PostProcessLifecycleChange(PostProcessLifecycleChangeKind.Away),
                new PostProcessMemoChange(new StateVersion(1), "must not apply")),
            new PostProcessCommitItem(
                second.CaseId,
                second.Version,
                new PostProcessLifecycleChange(PostProcessLifecycleChangeKind.Hit),
                null),
        ]);

        Assert.Equal(BulkItemStatus.Failed, results[0].Status);
        Assert.Equal(BulkItemStatus.Succeeded, results[1].Status);
        Assert.Equal(1, unitOfWork.Discards);
        Assert.Equal(1, unitOfWork.Saves);
        Assert.Equal(0, memos.SalesUpdates);
    }

    [Fact]
    public async Task Worklist_uses_visibility_seam_and_mine_is_the_agreed_or_predicate()
    {
        var visibility = new Visibility();
        var queries = new Queries();
        var useCase = new GetPostProcessWorklist(
            queries,
            visibility,
            new BusinessDate(Today),
            Current(Sales, UserRole.Sales));

        await useCase.ExecuteAsync(
            PostProcessPreset.Today,
            PostProcessScope.AllPermitted);

        Assert.Equal(1, visibility.Calls);
        Assert.Equal(PostProcessScope.AllPermitted, queries.Scope);
        Assert.True(PostProcessOwnership.IsMine(Sales, Sales, Trader, Trader));
        Assert.True(PostProcessOwnership.IsMine(Sales, null, Sales, Trader));
        Assert.True(PostProcessOwnership.IsMine(Sales, null, Trader, Sales));
        Assert.False(PostProcessOwnership.IsMine(
            Sales,
            UserId.Create("other-sales"),
            UserId.Create("owner"),
            Trader));
    }

    private static CommitPostProcessChanges Committer(
        Cases cases,
        Memos memos,
        UnitOfWork unitOfWork)
    {
        ICurrentUser current = Current(Sales, UserRole.Sales);
        var authorization = new RfqAuthorization();
        var events = new Events();
        var businessDate = new BusinessDate(Today);
        return new CommitPostProcessChanges(
            new CloseRfqOperation(
                cases,
                authorization,
                current,
                events,
                businessDate,
                TimeProvider.System),
            new CancelRfqOperation(
                cases,
                authorization,
                current,
                events,
                businessDate,
                TimeProvider.System),
            new CorrectOutcomeOperation(
                cases,
                authorization,
                current,
                events,
                businessDate,
                TimeProvider.System),
            new UpdateMemoOperation(
                cases,
                memos,
                new Users(),
                authorization,
                current),
            unitOfWork);
    }

    private static RfqCase Open(CaseId caseId)
    {
        var draft = RfqCase.CreateDraft(
            caseId,
            RevisionId.New(),
            ClientId.Create("client"),
            SecurityId.Create("security"),
            CategoryId.Create("category"),
            Trader,
            new RevisionTerms(
                1_000_000,
                Today.AddDays(2),
                Today.AddDays(2),
                "message"),
            Sales,
            Now,
            Sales);
        return RfqLifecycleTransitions.ConfirmInitial(
            draft,
            draft.CurrentRevision.Terms,
            Trader,
            Today,
            Sales,
            Now,
            draft.CurrentRevision.Version);
    }

    private static RfqCase Quoted(CaseId caseId)
    {
        RfqCase open = Open(caseId);
        WorkingQuote working = WorkingQuoteFactory.CreateInitialFor(open, Trader, Now);
        working = WorkingQuoteTransitions.ApplyCalculated(
            working,
            new CalculatedQuotePayload(
                CalculationDriver.Price,
                100,
                100,
                1,
                1,
                0,
                1,
                1,
                10,
                10),
            working.Version,
            Trader,
            Now);
        return QuoteTransitions.Confirm(
            open,
            working,
            QuoteId.New(),
            new QuoteConfirmation(Trader, Now, new QuoteExpiry.None())).Rfq;
    }

    private static CurrentUserService Current(UserId id, UserRole role) =>
        new(new CurrentUser(
            id,
            new HashSet<UserRole> { role },
            DeskId.Create("desk")));

    private sealed record CurrentUserService(CurrentUser User) : ICurrentUser;

    private sealed class Cases(params RfqCase[] values) : IRfqCaseRepository
    {
        private readonly Dictionary<CaseId, RfqCase> cases =
            values.ToDictionary(value => value.CaseId);

        public RfqCase Get(CaseId caseId) => cases[caseId];

        public void Add(RfqCase rfqCase) => cases.Add(rfqCase.CaseId, rfqCase);

        public Task<RfqCase?> GetAsync(
            CaseId caseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(cases.GetValueOrDefault(caseId));

        public void Update(RfqCase rfqCase) => cases[rfqCase.CaseId] = rfqCase;

        public void UpdateRevision(RfqRevision revision) { }
    }

    private sealed class Memos(params CaseId[] caseIds) : IRfqMemoRepository
    {
        private readonly Dictionary<CaseId, SalesMemo> sales =
            caseIds.ToDictionary(caseId => caseId, SalesMemo.Create);

        private readonly Dictionary<CaseId, TraderMemo> traders =
            caseIds.ToDictionary(caseId => caseId, TraderMemo.Create);

        public int SalesUpdates { get; private set; }

        public Task<SalesMemo?> GetSalesAsync(
            CaseId caseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SalesMemo?>(sales.GetValueOrDefault(caseId));

        public Task<TraderMemo?> GetTraderAsync(
            CaseId caseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TraderMemo?>(traders.GetValueOrDefault(caseId));

        public void Update(SalesMemo memo)
        {
            SalesUpdates++;
            sales[memo.CaseId] = memo;
        }

        public void Update(TraderMemo memo) => traders[memo.CaseId] = memo;
    }

    private sealed class Events : IRfqEventSink
    {
        public List<RfqTransition> Recorded { get; } = [];
        public void Record(RfqTransition transition) => Recorded.Add(transition);
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public int Discards { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves++;
            return Task.CompletedTask;
        }

        public void DiscardChanges() => Discards++;
    }

    private sealed record BusinessDate(DateOnly Value) : IBusinessDateProvider
    {
        public Task<DateOnly> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Value);
    }

    private sealed class Users : IUserDirectory
    {
        public Task<IReadOnlyList<UserSummary>> GetUsersAsync(
            UserRole? role = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSummary>>([]);

        public Task<UserSummary?> ResolveAsync(
            UserId userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserSummary?>(new UserSummary(
                userId,
                userId.Value,
                new HashSet<UserRole> { UserRole.Trader },
                DeskId.Create("desk")));
    }

    private sealed class Visibility : IPostProcessVisibility
    {
        public int Calls { get; private set; }

        public Task<IReadOnlySet<CaseId>> GetPermittedCaseIdsAsync(
            CurrentUser currentUser,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlySet<CaseId>>(
                new HashSet<CaseId> { new(1) });
        }
    }

    private sealed class Queries : IPostProcessQueries
    {
        public PostProcessScope? Scope { get; private set; }

        public Task<IReadOnlyList<PostProcessWorklistItem>> GetAsync(
            PostProcessPreset preset,
            PostProcessScope scope,
            DateOnly currentBusinessDate,
            CurrentUser currentUser,
            IReadOnlySet<CaseId> permittedCaseIds,
            CancellationToken cancellationToken = default)
        {
            Scope = scope;
            return Task.FromResult<IReadOnlyList<PostProcessWorklistItem>>([]);
        }
    }
}
