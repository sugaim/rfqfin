namespace Rfq.Application;

public sealed class BulkCloseAwayRfqs(CloseAwayRfq closeAway, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                CloseAwayRfqResult result = await closeAway.ExecuteAsync(
                    item.CaseId, item.ExpectedCurrentVersion, token);
                return result.Outcome == CloseAwayOutcome.ClosedAway
                    ? BulkActionOutcome.Succeeded
                    : BulkActionOutcome.Skipped;
            },
            unitOfWork,
            cancellationToken);
}

public sealed class BulkCancelRfqs(CancelRfq cancel, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await cancel.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, token);
                return BulkActionOutcome.Succeeded;
            },
            unitOfWork,
            cancellationToken);
}
