using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class CreateDraftTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveDraftAllowsIncompleteRevisionAndCommitsOnce()
    {
        var harness = new Harness();

        var result = await harness.CreateDraft.ExecuteAsync(new CreateDraftCommand(
            "client-1",
            "security-1",
            null,
            null,
            null,
            null));

        var created = Assert.Single(harness.Repository.Cases);
        Assert.Equal(101, result.CaseId);
        Assert.Equal("Draft", result.RfqStatus);
        Assert.Equal("Draft", result.RevisionStatus);
        Assert.Null(result.Notional);
        Assert.Null(result.SettlementDate);
        Assert.Equal("trader-1", result.AssignedTraderId);
        Assert.Equal(new DateOnly(2026, 9, 23), result.StandardSettlementDate);
        Assert.Equal(created.InitialRevision.RevisionId.Value, result.RevisionId);
        Assert.Equal(1, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ConfirmSavedDraftMovesToRequestedAndEnsuresWorkingQuote()
    {
        var harness = new Harness();
        var draft = await harness.CreateDraft.ExecuteAsync(ValidCommand());

        var result = await harness.ConfirmInitialDraft.ExecuteAsync(
            new UpdateInitialDraftCommand(
                draft.CaseId,
                200_000_000m,
                new DateOnly(2026, 9, 24),
                "Updated inquiry",
                "trader-2",
                draft.Version));

        Assert.Equal("Active", result.RfqStatus);
        Assert.Equal("Confirmed", result.RevisionStatus);
        Assert.Equal("Requested", result.QuoteStatus);
        Assert.Equal("Initial", result.QuoteRequestReason);
        Assert.Equal("trader-2", result.AssignedTraderId);
        Assert.Equal(200_000_000m, result.Notional);
        Assert.Equal(result.RevisionId, Assert.Single(harness.WorkingQuotes.Revisions).Value);
        Assert.Equal(2, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task DirectConfirmCreatesCaseAndWorkingQuoteAtomically()
    {
        var harness = new Harness();

        var result = await harness.ConfirmNewRfq.ExecuteAsync(ValidCommand());

        Assert.Single(harness.Repository.Cases);
        Assert.Single(harness.WorkingQuotes.Revisions);
        Assert.Equal("Active", result.RfqStatus);
        Assert.Equal("Confirmed", result.RevisionStatus);
        Assert.Equal("Requested", result.QuoteStatus);
        Assert.Equal("Initial", result.QuoteRequestReason);
        Assert.Equal(1, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task DiscardedInitialDraftCannotBeConfirmed()
    {
        var harness = new Harness();
        var draft = await harness.CreateDraft.ExecuteAsync(ValidCommand());

        await harness.DiscardInitialDraft.ExecuteAsync(draft.CaseId, draft.Version);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.ConfirmInitialDraft.ExecuteAsync(new UpdateInitialDraftCommand(
                draft.CaseId,
                100m,
                new DateOnly(2026, 9, 24),
                null,
                null,
                draft.Version + 1)));
    }

    private static CreateDraftCommand ValidCommand() => new(
        "client-1",
        "security-1",
        100_000_000m,
        new DateOnly(2026, 9, 24),
        "Customer inquiry",
        null);

    private sealed class Harness
    {
        public Harness()
        {
            var currentUser = new StubCurrentUser();
            var users = new StubUserDirectory();
            var systemDate = new StubSystemDateProvider();
            var timeProvider = new FixedTimeProvider(Now);
            var defaults = new ResolveRfqDefaults(
                new StubSecuritySearch(),
                new StubCategoryRouting(),
                users,
                systemDate,
                new StubSettlementResolver(),
                currentUser);
            var traderValidator = new AssignedTraderValidator(users, currentUser);
            var factory = new InitialRfqFactory(
                new StubCaseIdGenerator(101),
                new StubClientSearch(),
                defaults,
                traderValidator,
                currentUser,
                timeProvider);

            CreateDraft = new CreateDraft(factory, Repository, UnitOfWork);
            ConfirmInitialDraft = new ConfirmInitialDraft(
                Repository,
                traderValidator,
                WorkingQuotes,
                systemDate,
                currentUser,
                UnitOfWork,
                timeProvider);
            ConfirmNewRfq = new ConfirmNewRfq(
                factory,
                Repository,
                WorkingQuotes,
                systemDate,
                currentUser,
                UnitOfWork,
                timeProvider);
            DiscardInitialDraft = new DiscardInitialDraft(
                Repository,
                currentUser,
                UnitOfWork);
        }

        public RecordingRfqCaseRepository Repository { get; } = new();

        public RecordingUnitOfWork UnitOfWork { get; } = new();

        public RecordingWorkingQuoteEnsurer WorkingQuotes { get; } = new();

        public CreateDraft CreateDraft { get; }

        public ConfirmInitialDraft ConfirmInitialDraft { get; }

        public ConfirmNewRfq ConfirmNewRfq { get; }

        public DiscardInitialDraft DiscardInitialDraft { get; }
    }

    private sealed class StubCaseIdGenerator(long value) : ICaseIdGenerator
    {
        public Task<CaseId> NextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CaseId(value));
    }

    private sealed class RecordingRfqCaseRepository : IRfqCaseRepository
    {
        public List<RfqCase> Cases { get; } = [];

        public void Add(RfqCase rfqCase) => Cases.Add(rfqCase);

        public Task<RfqCase?> GetAsync(
            CaseId caseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Cases.SingleOrDefault(item => item.CaseId == caseId));

        public void Update(RfqCase rfqCase)
        {
        }

        public Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(
            UserId salesUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalesRfqListItem>>([]);
    }

    private sealed class RecordingWorkingQuoteEnsurer : IWorkingQuoteEnsurer
    {
        public List<RevisionId> Revisions { get; } = [];

        public Task EnsureAsync(
            RevisionId revisionId,
            UserId createdBy,
            DateTimeOffset createdAt,
            CancellationToken cancellationToken = default)
        {
            if (!Revisions.Contains(revisionId))
            {
                Revisions.Add(revisionId);
            }

            return Task.CompletedTask;
        }
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

    private sealed class StubCurrentUser : ICurrentUser
    {
        public CurrentUser User { get; } = new(
            UserId.Create("sales-1"),
            new HashSet<UserRole> { UserRole.Sales },
            "jpy-credit");
    }

    private sealed class StubSecuritySearch : ISecuritySearch
    {
        public Task<IReadOnlyList<SecuritySearchResult>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SecuritySearchResult>>([]);

        public Task<SecuritySearchResult?> ResolveAsync(
            SecurityId securityId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SecuritySearchResult?>(securityId.Value == "security-1"
                ? new(
                    "security-1",
                    "Government Bond",
                    "JGB 0.5 03/20/2030",
                    "0-02-0001-00001",
                    "JP0000000001",
                    "JGB",
                    "JGB")
                : null);
    }

    private sealed class StubClientSearch : IClientSearch
    {
        public Task<IReadOnlyList<ClientSearchResult>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ClientSearchResult>>([]);

        public Task<ClientSearchResult?> ResolveAsync(
            ClientId clientId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ClientSearchResult?>(clientId.Value == "client-1"
                ? new("client-1", "C001", "Client")
                : null);
    }

    private sealed class StubUserDirectory : IUserDirectory
    {
        private static readonly UserSummary[] Users =
        [
            new("sales-1", "Sales", new HashSet<UserRole> { UserRole.Sales }, "jpy-credit"),
            new("trader-1", "Trader 1", new HashSet<UserRole> { UserRole.Trader }, "jpy-credit"),
            new("trader-2", "Trader 2", new HashSet<UserRole> { UserRole.Trader }, "jpy-credit"),
        ];

        public Task<IReadOnlyList<UserSummary>> GetUsersAsync(
            UserRole? role = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSummary>>(
                Users.Where(user => role is null || user.Roles.Contains(role.Value)).ToArray());

        public Task<UserSummary?> ResolveAsync(
            UserId userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.SingleOrDefault(user => user.UserId == userId.Value));
    }

    private sealed class StubCategoryRouting : ICategoryRouting
    {
        public Task<UserId?> GetDefaultAssignedTraderAsync(
            CategoryId categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserId?>(UserId.Create("trader-1"));
    }

    private sealed class StubSettlementResolver : IStandardSettlementResolver
    {
        public DateOnly Resolve(SecurityId securityId, DateOnly systemDate) => systemDate.AddDays(2);
    }

    private sealed class StubSystemDateProvider : ISystemDateProvider
    {
        public Task<DateOnly> GetTodayAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DateOnly(2026, 9, 21));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
