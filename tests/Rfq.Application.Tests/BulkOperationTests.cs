using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class BulkOperationTests
{
    public static TheoryData<ExpectedRfqException, BulkFailureCode> SupportedErrors => new()
    {
        { new RfqRequestValidationException("validation"), BulkFailureCode.Validation },
        { new DomainRuleViolationException("state"), BulkFailureCode.InvalidState },
        { new StateVersionMismatchException("version"), BulkFailureCode.VersionConflict },
        { new RfqNotFoundException("missing"), BulkFailureCode.NotFound },
        { new RfqForbiddenException("forbidden"), BulkFailureCode.Forbidden },
    };

    public static TheoryData<Exception> FatalErrors =>
    [
        new CalculationFailureException(Guid.NewGuid(), "CALC", "calculation"),
        new InvalidOperationException("invalid operation"),
        new DomainInvariantException("domain invariant"),
        new RfqInvariantException("system invariant"),
        new Exception("unknown"),
    ];

    [Fact]
    public async Task Successful_items_commit_independently()
    {
        var unitOfWork = new UnitOfWork();
        IReadOnlyList<BulkItemResult> results = await BulkOperation.ExecuteAsync(
            [new CaseId(1), new CaseId(2), new CaseId(3)],
            item => item,
            async (_, token) =>
            {
                await unitOfWork.SaveChangesAsync(token);
                return BulkActionOutcome.Succeeded;
            },
            unitOfWork,
            CancellationToken.None);

        Assert.All(results, result => Assert.Equal(BulkItemStatus.Succeeded, result.Status));
        Assert.Equal(3, unitOfWork.Saves);
    }

    [Fact]
    public async Task Mixed_results_continue_after_recoverable_failure_and_discard_state()
    {
        var unitOfWork = new UnitOfWork();
        CaseId[] items = [new CaseId(1), new CaseId(2), new CaseId(3), new CaseId(4)];

        IReadOnlyList<BulkItemResult> results = await BulkOperation.ExecuteAsync(
            items,
            item => item,
            (item, _) => item.Value switch
            {
                1 => Task.FromResult(BulkActionOutcome.Succeeded),
                2 => Task.FromResult(BulkActionOutcome.Skipped),
                3 => Task.FromException<BulkActionOutcome>(
                    new StateVersionMismatchException("changed")),
                _ => Task.FromResult(BulkActionOutcome.Succeeded),
            },
            unitOfWork,
            CancellationToken.None);

        Assert.Equal(
            [BulkItemStatus.Succeeded, BulkItemStatus.Skipped,
                BulkItemStatus.Failed, BulkItemStatus.Succeeded],
            results.Select(result => result.Status));
        Assert.Equal(BulkFailureCode.VersionConflict, results[2].Code);
        Assert.Equal(1, unitOfWork.Discards);
    }

    [Theory]
    [MemberData(nameof(SupportedErrors))]
    public async Task Supported_error_kind_is_failed_discarded_and_next_item_runs(
        ExpectedRfqException exception,
        BulkFailureCode expectedCode)
    {
        var unitOfWork = new UnitOfWork();
        var executed = new List<long>();

        IReadOnlyList<BulkItemResult> results = await BulkOperation.ExecuteAsync(
            [new CaseId(1), new CaseId(2)],
            item => item,
            (item, _) =>
            {
                executed.Add(item.Value);
                return item.Value == 1
                    ? Task.FromException<BulkActionOutcome>(exception)
                    : Task.FromResult(BulkActionOutcome.Succeeded);
            },
            unitOfWork,
            CancellationToken.None);

        Assert.Equal([1L, 2L], executed);
        Assert.Equal(BulkItemStatus.Failed, results[0].Status);
        Assert.Equal(expectedCode, results[0].Code);
        Assert.Equal(BulkItemStatus.Succeeded, results[1].Status);
        Assert.Equal(1, unitOfWork.Discards);
    }

    [Theory]
    [MemberData(nameof(FatalErrors))]
    public async Task Unsupported_or_unexpected_error_aborts_without_running_next_item(
        Exception exception)
    {
        var unitOfWork = new UnitOfWork();
        var executed = new List<long>();

        Exception thrown = await Assert.ThrowsAsync(
            exception.GetType(),
            () => BulkOperation.ExecuteAsync(
                [new CaseId(1), new CaseId(2)],
                item => item,
                (item, _) =>
                {
                    executed.Add(item.Value);
                    return item.Value == 1
                        ? Task.FromException<BulkActionOutcome>(exception)
                        : Task.FromResult(BulkActionOutcome.Succeeded);
                },
                unitOfWork,
                CancellationToken.None));

        Assert.Same(exception, thrown);
        Assert.Equal([1L], executed);
        Assert.Equal(0, unitOfWork.Discards);
    }

    [Fact]
    public async Task Request_cancellation_aborts_bulk()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BulkOperation.ExecuteAsync(
            [new CaseId(1)],
            item => item,
            (_, _) => Task.FromResult(BulkActionOutcome.Succeeded),
            new UnitOfWork(),
            cancellation.Token));
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
}
