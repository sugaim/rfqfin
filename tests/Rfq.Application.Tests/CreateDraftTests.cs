using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class CreateDraftTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteCreatesInitialDraftWithResolvedDefaultsAndCommitsOnce()
    {
        var repository = new RecordingRfqCaseRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var useCase = CreateUseCase(repository, unitOfWork, new StubCaseIdGenerator(101));

        var result = await useCase.ExecuteAsync(new CreateDraftCommand(
            "client-1",
            "security-1",
            new DateOnly(2026, 9, 24),
            null));

        var created = Assert.Single(repository.Added);
        Assert.Equal(101, result.CaseId);
        Assert.Equal(result.CaseId, created.CaseId.Value);
        Assert.Equal(result.RevisionId, created.InitialRevision.RevisionId.Value);
        Assert.Equal("Draft", result.RfqStatus);
        Assert.Equal("JGB", result.CategoryId);
        Assert.Equal("sales-1", result.ContactOwnerId);
        Assert.Equal("trader-1", result.AssignedTraderId);
        Assert.Equal(new DateOnly(2026, 9, 23), result.StandardSettlementDate);
        Assert.Equal(Now, result.CreatedAt);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteAllowsSalesToOverrideAssignedTraderOnSameDesk()
    {
        var repository = new RecordingRfqCaseRepository();
        var useCase = CreateUseCase(
            repository,
            new RecordingUnitOfWork(),
            new StubCaseIdGenerator(101));

        var result = await useCase.ExecuteAsync(new CreateDraftCommand(
            "client-1",
            "security-1",
            new DateOnly(2026, 9, 24),
            "trader-2"));

        Assert.Equal("trader-2", result.AssignedTraderId);
    }

    [Theory]
    [InlineData("", "security-1")]
    [InlineData("client-1", "")]
    [InlineData("   ", "security-1")]
    [InlineData("client-1", "   ")]
    public async Task ExecuteRejectsMissingClientOrSecurity(
        string clientId,
        string securityId)
    {
        var repository = new RecordingRfqCaseRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var caseIdGenerator = new StubCaseIdGenerator(101);
        var useCase = CreateUseCase(repository, unitOfWork, caseIdGenerator);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(new CreateDraftCommand(
                clientId,
                securityId,
                new DateOnly(2026, 9, 24),
                null)));

        Assert.Empty(repository.Added);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, caseIdGenerator.CallCount);
    }

    private static CreateDraft CreateUseCase(
        RecordingRfqCaseRepository repository,
        RecordingUnitOfWork unitOfWork,
        StubCaseIdGenerator caseIdGenerator)
    {
        var currentUser = new StubCurrentUser();
        var users = new StubUserDirectory();
        var securitySearch = new StubSecuritySearch();
        var defaults = new ResolveRfqDefaults(
            securitySearch,
            new StubCategoryRouting(),
            users,
            new StubSystemDateProvider(),
            new StubSettlementResolver(),
            currentUser);
        return new CreateDraft(
            caseIdGenerator,
            repository,
            unitOfWork,
            new StubClientSearch(),
            users,
            defaults,
            currentUser,
            new FixedTimeProvider(Now));
    }

    private sealed class StubCaseIdGenerator(long value) : ICaseIdGenerator
    {
        public int CallCount { get; private set; }

        public Task<CaseId> NextAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new CaseId(value));
        }
    }

    private sealed class RecordingRfqCaseRepository : IRfqCaseRepository
    {
        public List<RfqCase> Added { get; } = [];

        public void Add(RfqCase rfqCase) => Added.Add(rfqCase);

        public Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(
            UserId salesUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalesRfqListItem>>([]);
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
                    "国債",
                    "JGB 0.5 03/20/2030",
                    "0-02-0001-00001",
                    "JP0000000001",
                    "JGB",
                    "日本国債")
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
                ? new("client-1", "C001", "顧客")
                : null);
    }

    private sealed class StubUserDirectory : IUserDirectory
    {
        private static readonly UserSummary[] Users =
        [
            new("sales-1", "営業", new HashSet<UserRole> { UserRole.Sales }, "jpy-credit"),
            new("trader-1", "担当1", new HashSet<UserRole> { UserRole.Trader }, "jpy-credit"),
            new("trader-2", "担当2", new HashSet<UserRole> { UserRole.Trader }, "jpy-credit"),
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
