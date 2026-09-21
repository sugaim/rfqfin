using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class TraderOwnershipTests
{
    [Fact]
    public async Task OtherAssignedUnownedRfqRequiresConfirmationBeforePickUp()
    {
        var rfqCase = CreateOpenCase("trader-b");
        var currentUser = new StubCurrentUser("trader-a", UserRole.Trader);
        var repository = new StubRepository(rfqCase);
        var useCase = new PickUpRfq(
            repository,
            new RfqAuthorization(),
            currentUser,
            new StubUnitOfWork());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(10, rfqCase.CurrentVersion, false));

        var result = await useCase.ExecuteAsync(10, rfqCase.CurrentVersion, true);
        Assert.True(result.Owned);
        Assert.Equal("trader-a", result.AssignedTraderId);
    }

    [Fact]
    public async Task OnlyOwnerCanRelease()
    {
        var rfqCase = CreateOpenCase("trader-a");
        rfqCase.PickUp(UserId.Create("trader-a"), rfqCase.CurrentVersion);
        var repository = new StubRepository(rfqCase);
        var forbidden = new ReleaseRfq(
            repository,
            new RfqAuthorization(),
            new StubCurrentUser("trader-b", UserRole.Trader),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            forbidden.ExecuteAsync(10, rfqCase.CurrentVersion));

        var allowed = new ReleaseRfq(
            repository,
            new RfqAuthorization(),
            new StubCurrentUser("trader-a", UserRole.Trader),
            new StubUnitOfWork());
        var result = await allowed.ExecuteAsync(10, rfqCase.CurrentVersion);
        Assert.False(result.Owned);
        Assert.Equal("trader-a", result.AssignedTraderId);
    }

    [Fact]
    public async Task AssignAndConfirmedTakeOverFollowRoutingRules()
    {
        var rfqCase = CreateOpenCase("trader-a");
        var repository = new StubRepository(rfqCase);
        var traderA = new StubCurrentUser("trader-a", UserRole.Trader);
        var assign = new AssignTrader(
            repository,
            new AssignedTraderValidator(new StubUserDirectory(), traderA),
            new RfqAuthorization(),
            traderA,
            new StubUnitOfWork());

        var assigned = await assign.ExecuteAsync(
            10,
            "trader-b",
            rfqCase.CurrentVersion);
        Assert.False(assigned.Owned);
        Assert.Equal("trader-b", assigned.AssignedTraderId);

        rfqCase.PickUp(UserId.Create("trader-b"), rfqCase.CurrentVersion);
        var takeOver = new TakeOverRfq(
            repository,
            new RfqAuthorization(),
            traderA,
            new StubUnitOfWork());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            takeOver.ExecuteAsync(10, rfqCase.CurrentVersion, false));

        var taken = await takeOver.ExecuteAsync(10, rfqCase.CurrentVersion, true);
        Assert.True(taken.Owned);
        Assert.Equal("trader-a", taken.AssignedTraderId);
    }

    [Fact]
    public async Task SalesRoleCannotPerformTraderOwnershipOperation()
    {
        var rfqCase = CreateOpenCase("trader-a");
        var useCase = new PickUpRfq(
            new StubRepository(rfqCase),
            new RfqAuthorization(),
            new StubCurrentUser("sales-1", UserRole.Sales),
            new StubUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(10, rfqCase.CurrentVersion, false));
    }

    private static RfqCase CreateOpenCase(string assignedTraderId)
    {
        var sales = UserId.Create("sales-1");
        var rfqCase = RfqCase.CreateDraft(
            new CaseId(10),
            ClientId.Create("client-1"),
            SecurityId.Create("security-1"),
            CategoryId.Create("JGB"),
            UserId.Create(assignedTraderId),
            100_000_000m,
            new DateOnly(2026, 9, 24),
            new DateOnly(2026, 9, 23),
            null,
            sales,
            DateTimeOffset.UtcNow);
        rfqCase.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            sales,
            DateTimeOffset.UtcNow,
            rfqCase.InitialRevision.Version);
        return rfqCase;
    }

    private sealed class StubRepository(RfqCase rfqCase) : IRfqCaseRepository
    {
        public void Add(RfqCase item) => throw new NotSupportedException();

        public Task<RfqCase?> GetAsync(
            CaseId caseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RfqCase?>(rfqCase.CaseId == caseId ? rfqCase : null);

        public void Update(RfqCase item)
        {
        }

        public Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(
            UserId salesUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalesRfqListItem>>([]);

        public Task<IReadOnlyList<TraderRfqListItem>> GetActiveTraderRfqsAsync(
            string deskId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TraderRfqListItem>>([]);
    }

    private sealed class StubCurrentUser(string userId, UserRole role) : ICurrentUser
    {
        public CurrentUser User { get; } = new(
            UserId.Create(userId),
            new HashSet<UserRole> { role },
            "jpy-credit");
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubUserDirectory : IUserDirectory
    {
        private static readonly UserSummary[] Traders =
        [
            new("trader-a", "Trader A", new HashSet<UserRole> { UserRole.Trader }, "jpy-credit"),
            new("trader-b", "Trader B", new HashSet<UserRole> { UserRole.Trader }, "jpy-credit"),
        ];

        public Task<IReadOnlyList<UserSummary>> GetUsersAsync(
            UserRole? role = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSummary>>(Traders);

        public Task<UserSummary?> ResolveAsync(
            UserId userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserSummary?>(
                Traders.SingleOrDefault(item => item.UserId == userId.Value));
    }
}
