using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class WorkingQuoteWorkflowTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessfulCalculationUpdatesPayloadAndSlide()
    {
        var harness = new Harness();

        var result = await harness.UseCase.ExecuteAsync(
            20,
            CalculationDriver.Price,
            99m,
            0.05m,
            harness.Case.CurrentVersion,
            harness.Quote.Version);

        Assert.Equal(2, result.Version);
        Assert.Equal(0.05m, result.Calculated?.SimpleYieldSlide);
        Assert.Equal(1.25m, result.Calculated?.FinalSimpleYield);
        Assert.Equal(1, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CalculationFailureLogsAttemptAndLeavesQuoteUnchanged()
    {
        var harness = new Harness(error: true);

        var exception = await Assert.ThrowsAsync<CalculationFailureException>(() =>
            harness.UseCase.ExecuteAsync(
                20,
                CalculationDriver.GSpread,
                -999m,
                0m,
                harness.Case.CurrentVersion,
                harness.Quote.Version));

        Assert.Equal("TEST_FAILURE", exception.Code);
        Assert.Equal(1, harness.Quote.Version);
        Assert.Null(harness.Quote.Calculated);
        Assert.Single(harness.WorkingQuotes.Failures);
        Assert.Equal(1, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ResultIsRejectedWhenOwnershipChangesDuringCalculation()
    {
        Harness? harness = null;
        harness = new Harness(onCalculate: () =>
            harness!.Case.Release(
                UserId.Create("trader-a"),
                harness.Case.CurrentVersion));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.UseCase.ExecuteAsync(
                20,
                CalculationDriver.SimpleYield,
                1.2m,
                0m,
                3,
                1));

        Assert.Equal(1, harness.Quote.Version);
    }

    [Fact]
    public async Task ResultIsRejectedWhenCurrentRevisionDiffersFromRequestedRevision()
    {
        var harness = new Harness(contextRevisionId: Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.UseCase.ExecuteAsync(
                20,
                CalculationDriver.Price,
                100m,
                0m,
                harness.Case.CurrentVersion,
                harness.Quote.Version));

        Assert.Equal(1, harness.Quote.Version);
    }

    private sealed class Harness
    {
        public Harness(
            bool error = false,
            Action? onCalculate = null,
            Guid? contextRevisionId = null)
        {
            Case = CreateOwnedCase();
            Quote = WorkingQuote.CreateEmpty(
                Case.InitialRevision.RevisionId,
                UserId.Create("trader-a"),
                Now);
            WorkingQuotes = new StubWorkingQuoteRepository(
                Case,
                Quote,
                contextRevisionId ?? Case.InitialRevision.RevisionId.Value);
            UnitOfWork = new RecordingUnitOfWork();
            UseCase = new CalculateWorkingQuote(
                new StubRfqRepository(Case),
                WorkingQuotes,
                new StubCalculationClient(error, onCalculate),
                new RfqAuthorization(),
                new StubCurrentUser(),
                UnitOfWork,
                new FixedTimeProvider());
        }

        public RfqCase Case { get; }
        public WorkingQuote Quote { get; }
        public StubWorkingQuoteRepository WorkingQuotes { get; }
        public RecordingUnitOfWork UnitOfWork { get; }
        public CalculateWorkingQuote UseCase { get; }
    }

    private static RfqCase CreateOwnedCase()
    {
        var sales = UserId.Create("sales-1");
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
        rfqCase.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            sales,
            Now,
            rfqCase.InitialRevision.Version);
        rfqCase.PickUp(UserId.Create("trader-a"), rfqCase.CurrentVersion);
        return rfqCase;
    }

    private sealed class StubCalculationClient(bool error, Action? onCalculate)
        : ICalculationClient
    {
        public Task<IReadOnlyList<CalculationResult>> CalculateBulkAsync(
            IReadOnlyList<CalculationRequest> requests,
            CancellationToken cancellationToken = default)
        {
            onCalculate?.Invoke();
            IReadOnlyList<CalculationResult> results = requests.Select(request =>
                error
                    ? (CalculationResult)new CalculationError(
                        request.RequestId,
                        "TEST_FAILURE",
                        "Forced test failure")
                    : new CalculationSuccess(
                        request.RequestId,
                        new CalculatedQuotePayload(
                            request.Driver,
                            request.Parameter.Value,
                            99m,
                            1.1m,
                            1.2m,
                            request.SimpleYieldSlide,
                            1.2m + request.SimpleYieldSlide,
                            1.22m,
                            50m,
                            53m))).ToArray();
            return Task.FromResult(results);
        }
    }

    private sealed class StubWorkingQuoteRepository(
        RfqCase rfqCase,
        WorkingQuote quote,
        Guid contextRevisionId) : IWorkingQuoteRepository
    {
        public List<CalculationFailureRecord> Failures { get; } = [];

        public Task<QuoteEditContext?> GetEditContextAsync(
            CaseId caseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<QuoteEditContext?>(new(
                rfqCase.CaseId.Value,
                contextRevisionId,
                rfqCase.SecurityId.Value,
                rfqCase.InitialRevision.SettlementDate!.Value,
                rfqCase.CurrentVersion,
                rfqCase.AssignedTraderId.Value,
                rfqCase.Owned,
                rfqCase.QuoteStatus!.Value.ToString(),
                quote));

        public Task<WorkingQuote?> GetAsync(
            RevisionId revisionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkingQuote?>(quote);

        public void Update(WorkingQuote workingQuote)
        {
        }

        public void AddFailure(CalculationFailureRecord failure) => Failures.Add(failure);
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

    private sealed class StubCurrentUser : ICurrentUser
    {
        public CurrentUser User { get; } = new(
            UserId.Create("trader-a"),
            new HashSet<UserRole> { UserRole.Trader },
            "jpy-credit");
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
