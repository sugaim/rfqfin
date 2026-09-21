namespace Rfq.Application;

public sealed class BulkPresentQuotes(PresentQuote present, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(items, item => item.CaseId, async (item, token) =>
        {
            await present.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, token);
            return BulkActionOutcome.Succeeded;
        }, unitOfWork, cancellationToken);
}

public sealed class BulkUnpresentQuotes(UnpresentQuote unpresent, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(items, item => item.CaseId, async (item, token) =>
        {
            await unpresent.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, token);
            return BulkActionOutcome.Succeeded;
        }, unitOfWork, cancellationToken);
}
