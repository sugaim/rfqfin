namespace Rfq.Application;

public sealed class PresentRfqs(PresentRfq present, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await present.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}

public sealed class UnpresentRfqs(UnpresentRfq unpresent, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await unpresent.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}
