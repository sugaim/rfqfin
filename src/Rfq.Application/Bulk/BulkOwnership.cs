using Rfq.Domain;

namespace Rfq.Application;

public sealed record PickUpRfqItem(CaseId CaseId, StateVersion ExpectedVersion, bool Confirmed);
public sealed record OwnershipBulkItem(CaseId CaseId, StateVersion ExpectedVersion);

public sealed class BulkPickUpRfqs(PickUpRfq pickUp, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<PickUpRfqItem> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(items, item => item.CaseId, async (item, token) =>
        {
            await pickUp.ExecuteAsync(item.CaseId, item.ExpectedVersion, item.Confirmed, token);
            return BulkActionOutcome.Succeeded;
        }, unitOfWork, cancellationToken);
}

public sealed class BulkReleaseRfqs(ReleaseRfq release, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<OwnershipBulkItem> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(items, item => item.CaseId, async (item, token) =>
        {
            await release.ExecuteAsync(item.CaseId, item.ExpectedVersion, token);
            return BulkActionOutcome.Succeeded;
        }, unitOfWork, cancellationToken);
}

public sealed class BulkAssignTrader(AssignTrader assign, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        UserId targetAssignedTraderId,
        IReadOnlyList<OwnershipBulkItem> items,
        CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(items, item => item.CaseId, async (item, token) =>
        {
            var result = await assign.ExecuteAsync(
                item.CaseId, targetAssignedTraderId, item.ExpectedVersion, token);
            return result.Outcome == AssignTraderOutcome.Assigned
                ? BulkActionOutcome.Succeeded
                : BulkActionOutcome.Skipped;
        }, unitOfWork, cancellationToken);
}
