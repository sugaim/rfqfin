namespace Rfq.Application;

public sealed class CloseAwayRfqs(CloseAwayRfq closeAway, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                CloseAwayRfqResult result = await closeAway.ExecuteAsync(
                    item.CaseId, item.ExpectedCurrentVersion, token);
                return result.Outcome == CloseAwayOutcome.ClosedAway
                    ? CaseOperationOutcome.Applied
                    : CaseOperationOutcome.NoChange;
            },
            unitOfWork,
            cancellationToken);
}

public sealed class CancelRfqs(CancelRfq cancel, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await cancel.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}

public sealed class ReopenRfqs(ReopenRfq reopen, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await reopen.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}
