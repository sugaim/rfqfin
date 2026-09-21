using Rfq.Domain;
using Xunit;

namespace Rfq.Application.Tests;

public sealed class BulkOperationTests
{
    [Fact]
    public async Task Successful_items_commit_independently()
    {
        var unitOfWork = new UnitOfWork();
        var results = await BulkOperation.ExecuteAsync(
            new[] { new CaseId(1), new CaseId(2), new CaseId(3) }, item => item,
            async (_, token) =>
            {
                await unitOfWork.SaveChangesAsync(token);
                return BulkActionOutcome.Succeeded;
            }, unitOfWork, CancellationToken.None);

        Assert.All(results, result => Assert.Equal(BulkItemStatus.Succeeded, result.Status));
        Assert.Equal(3, unitOfWork.Saves);
    }

    [Fact]
    public async Task Mixed_results_continue_after_recoverable_failure_and_discard_state()
    {
        var unitOfWork = new UnitOfWork();
        var items = new[] { new CaseId(1), new CaseId(2), new CaseId(3), new CaseId(4) };

        var results = await BulkOperation.ExecuteAsync(
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

    [Fact]
    public async Task Fatal_exception_aborts_without_being_converted_to_item_failure()
    {
        var unitOfWork = new UnitOfWork();
        await Assert.ThrowsAsync<DomainInvariantException>(() => BulkOperation.ExecuteAsync(
            [new CaseId(1), new CaseId(2)], item => item,
            (item, _) => item.Value == 1
                ? Task.FromException<BulkActionOutcome>(new DomainInvariantException("fatal"))
                : Task.FromResult(BulkActionOutcome.Succeeded),
            unitOfWork, CancellationToken.None));
        Assert.Equal(0, unitOfWork.Discards);
    }

    [Fact]
    public async Task Request_cancellation_aborts_bulk()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BulkOperation.ExecuteAsync(
            [new CaseId(1)], item => item,
            (_, _) => Task.FromResult(BulkActionOutcome.Succeeded),
            new UnitOfWork(), cancellation.Token));
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
