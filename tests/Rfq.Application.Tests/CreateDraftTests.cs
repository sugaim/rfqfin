using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class CreateDraftTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteCreatesInitialDraftAndCommitsOnce()
    {
        var repository = new RecordingRfqCaseRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var useCase = new CreateDraft(
            new StubCaseIdGenerator(101),
            repository,
            unitOfWork,
            new StubCurrentUser(),
            new FixedTimeProvider(Now));

        var result = await useCase.ExecuteAsync(
            new CreateDraftCommand("client-1", "security-1"));

        var created = Assert.Single(repository.Added);
        Assert.Equal(101, result.CaseId);
        Assert.Equal(result.CaseId, created.CaseId.Value);
        Assert.Equal(result.RevisionId, created.InitialRevision.RevisionId.Value);
        Assert.Equal("Draft", result.RfqStatus);
        Assert.Equal(Now, result.CreatedAt);
        Assert.Equal(1, unitOfWork.SaveCount);
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
        var useCase = new CreateDraft(
            caseIdGenerator,
            repository,
            unitOfWork,
            new StubCurrentUser(),
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(new CreateDraftCommand(clientId, securityId)));

        Assert.Empty(repository.Added);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, caseIdGenerator.CallCount);
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
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SalesRfqListItem> result = [];
            return Task.FromResult(result);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
