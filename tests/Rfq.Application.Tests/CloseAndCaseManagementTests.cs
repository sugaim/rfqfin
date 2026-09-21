using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class CloseAndCaseManagementTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OnlyContactOwnerCanCloseAndCloseRecordsTransition()
    {
        var rfqCase = CreateQuotedCase(1);
        var state = new State(rfqCase);
        var forbidden = CreateClose(state, Current("sales-a", UserRole.Sales));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => forbidden.ExecuteAsync(
            1,
            RfqStatus.Hit,
            rfqCase.CurrentVersion));

        var useCase = CreateClose(state, Current("sales-dev", UserRole.Sales));
        var result = await useCase.ExecuteAsync(
            1,
            RfqStatus.Hit,
            rfqCase.CurrentVersion);

        Assert.Equal("Hit", result.RfqStatus);
        Assert.False(result.Owned);
        Assert.Equal(rfqCase.ClosedQuoteId?.Value, result.ClosedQuoteId);
        Assert.Equal(RfqTransitionKind.ClosedHit, Assert.Single(state.Events.Items).Kind);
        Assert.Equal(1, state.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task BulkCloseSkipsClosedCaseWithoutChangingOutcome()
    {
        var open = CreateQuotedCase(1);
        var closed = CreateQuotedCase(2);
        closed.Close(RfqStatus.Away, closed.CurrentVersion);
        var state = new State(open, closed);
        var useCase = new BulkCloseRfqs(
            state.Cases,
            new RfqAuthorization(),
            Current("sales-dev", UserRole.Sales),
            state.Events,
            state.UnitOfWork,
            new FixedTimeProvider());

        var results = await useCase.ExecuteAsync(
            [
                new BulkCloseItem(1, open.CurrentVersion),
                new BulkCloseItem(2, closed.CurrentVersion),
            ],
            RfqStatus.Hit);

        Assert.Equal("Closed", results[0].Result);
        Assert.Equal("Skipped", results[1].Result);
        Assert.Equal(RfqStatus.Away, closed.Status);
        Assert.Single(state.Events.Items);
    }

    [Fact]
    public async Task OutcomeCorrectionAndContactOwnerHandoffRequireCurrentOwner()
    {
        var rfqCase = CreateQuotedCase(1);
        rfqCase.Close(RfqStatus.Hit, rfqCase.CurrentVersion);
        var state = new State(rfqCase);
        var correction = new CorrectRfqOutcome(
            state.Cases,
            new RfqAuthorization(),
            Current("sales-dev", UserRole.Sales),
            state.Events,
            state.UnitOfWork,
            new FixedTimeProvider());

        await correction.ExecuteAsync(
            1,
            RfqStatus.Away,
            "customer corrected",
            rfqCase.CurrentVersion);
        Assert.Equal(RfqStatus.Away, rfqCase.Status);

        var handoff = new ChangeContactOwner(
            state.Cases,
            state.Users,
            new RfqAuthorization(),
            Current("sales-dev", UserRole.Sales),
            state.Events,
            state.UnitOfWork,
            new FixedTimeProvider());
        var result = await handoff.ExecuteAsync(
            1,
            "trader-a",
            rfqCase.CurrentVersion,
            true);

        Assert.Equal("trader-a", result.ContactOwnerId);
        Assert.Equal(UserId.Create("trader-a"), rfqCase.ContactOwnerId);
        var forbidden = new ChangeContactOwner(
            state.Cases,
            state.Users,
            new RfqAuthorization(),
            Current("sales-dev", UserRole.Sales),
            state.Events,
            state.UnitOfWork,
            new FixedTimeProvider());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => forbidden.ExecuteAsync(
            1,
            "sales-a",
            rfqCase.CurrentVersion,
            true));
    }

    [Fact]
    public async Task RoleScopedMemosRemainEditableAfterClose()
    {
        var rfqCase = CreateQuotedCase(1);
        rfqCase.Close(RfqStatus.Away, rfqCase.CurrentVersion);
        var state = new State(rfqCase);
        var salesMemo = new UpdateSalesMemo(
            state.Cases,
            state.Memos,
            state.Users,
            new RfqAuthorization(),
            Current("sales-a", UserRole.Sales),
            state.UnitOfWork);

        var salesResult = await salesMemo.ExecuteAsync(1, "follow up", 1);
        Assert.Equal("follow up", salesResult.Memo);

        var forbidden = new UpdateSalesMemo(
            state.Cases,
            state.Memos,
            state.Users,
            new RfqAuthorization(),
            Current("trader-a", UserRole.Trader),
            state.UnitOfWork);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            forbidden.ExecuteAsync(1, "not allowed", salesResult.Version));

        var traderMemo = new UpdateTraderMemo(
            state.Cases,
            state.Memos,
            state.Users,
            new RfqAuthorization(),
            Current("trader-a", UserRole.Trader),
            state.UnitOfWork);
        var traderResult = await traderMemo.ExecuteAsync(
            1,
            "desk note",
            salesResult.Version);
        Assert.Equal("desk note", traderResult.Memo);
    }

    private static CloseRfq CreateClose(State state, ICurrentUser user) => new(
        state.Cases,
        new RfqAuthorization(),
        user,
        state.Events,
        state.UnitOfWork,
        new FixedTimeProvider());

    private static RfqCase CreateQuotedCase(long caseId)
    {
        var sales = UserId.Create("sales-dev");
        var rfqCase = RfqCase.CreateDraft(
            new CaseId(caseId),
            ClientId.Create($"client-{caseId}"),
            SecurityId.Create($"security-{caseId}"),
            CategoryId.Create("JGB"),
            UserId.Create("trader-a"),
            100_000_000m,
            new DateOnly(2026, 9, 24),
            new DateOnly(2026, 9, 23),
            null,
            sales,
            Now);
        rfqCase.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            sales,
            Now,
            rfqCase.InitialRevision.Version);
        rfqCase.PickUp(UserId.Create("trader-a"), rfqCase.CurrentVersion);
        rfqCase.ConfirmQuote(
            QuoteId.New(),
            rfqCase.InitialRevision.RevisionId,
            rfqCase.CurrentVersion);
        return rfqCase;
    }

    private static ICurrentUser Current(string id, UserRole role) =>
        new StubCurrentUser(new CurrentUser(
            UserId.Create(id),
            new HashSet<UserRole> { role },
            "jpy-credit"));

    private sealed class State
    {
        public State(params RfqCase[] cases)
        {
            Cases = new StubRfqRepository(cases);
            Memos = new StubMemoRepository(cases.Select(item => CaseMemo.Create(item.CaseId)));
        }

        public StubRfqRepository Cases { get; }
        public StubMemoRepository Memos { get; }
        public StubUserDirectory Users { get; } = new();
        public RecordingEventSink Events { get; } = new();
        public RecordingUnitOfWork UnitOfWork { get; } = new();
    }

    private sealed class StubCurrentUser(CurrentUser user) : ICurrentUser
    {
        public CurrentUser User { get; } = user;
    }

    private sealed class StubRfqRepository(IEnumerable<RfqCase> cases) : IRfqCaseRepository
    {
        private readonly Dictionary<long, RfqCase> items = cases.ToDictionary(item => item.CaseId.Value);
        public void Add(RfqCase rfqCase) => items.Add(rfqCase.CaseId.Value, rfqCase);
        public Task<RfqCase?> GetAsync(CaseId caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult(items.GetValueOrDefault(caseId.Value));
        public void Update(RfqCase rfqCase) => items[rfqCase.CaseId.Value] = rfqCase;
        public Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(UserId user, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalesRfqListItem>>([]);
        public Task<IReadOnlyList<TraderRfqListItem>> GetActiveTraderRfqsAsync(string deskId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TraderRfqListItem>>([]);
    }

    private sealed class StubMemoRepository(IEnumerable<CaseMemo> memos) : ICaseMemoRepository
    {
        private readonly Dictionary<long, CaseMemo> items = memos.ToDictionary(item => item.CaseId.Value);
        public Task<CaseMemo?> GetAsync(CaseId caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult(items.GetValueOrDefault(caseId.Value));
        public void Update(CaseMemo memo) => items[memo.CaseId.Value] = memo;
    }

    private sealed class StubUserDirectory : IUserDirectory
    {
        private readonly UserSummary[] users =
        [
            new("sales-a", "Sales A", new HashSet<UserRole> { UserRole.Sales }, "jpy-credit"),
            new("trader-a", "Trader A", new HashSet<UserRole> { UserRole.Trader }, "jpy-credit"),
        ];
        public Task<IReadOnlyList<UserSummary>> GetUsersAsync(UserRole? role = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSummary>>(users);
        public Task<UserSummary?> ResolveAsync(UserId userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(users.SingleOrDefault(item => item.UserId == userId.Value));
    }

    private sealed class RecordingEventSink : IRfqEventSink
    {
        public List<RfqTransition> Items { get; } = [];
        public void Record(RfqTransition transition) => Items.Add(transition);
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
