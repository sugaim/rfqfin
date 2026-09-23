using Rfq.Domain;

namespace Rfq.Application;

public sealed record OwnershipItem(CaseId CaseId, StateVersion ExpectedCurrentVersion);

public sealed class PickUpRfqs(PickUpRfq pickUp, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        bool confirmed,
        IReadOnlyList<OwnershipItem> items,
        CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await pickUp.ExecuteAsync(
                    item.CaseId, item.ExpectedCurrentVersion, confirmed, token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}

public sealed class ReleaseRfqs(ReleaseRfq release, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<OwnershipItem> items, CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await release.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}

public sealed class AssignTraders(AssignTrader assign, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        UserId targetAssignedTraderId,
        IReadOnlyList<OwnershipItem> items,
        CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                AssignTraderResult result = await assign.ExecuteAsync(
                    item.CaseId, targetAssignedTraderId, item.ExpectedCurrentVersion, token);
                return result.Outcome == AssignTraderOutcome.Assigned
                    ? CaseOperationOutcome.Applied
                    : CaseOperationOutcome.NoChange;
            },
            unitOfWork,
            cancellationToken);
}

public sealed class TakeOverRfqs(TakeOverRfq takeOver, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        bool confirmed,
        IReadOnlyList<OwnershipItem> items,
        CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await takeOver.ExecuteAsync(
                    item.CaseId, item.ExpectedCurrentVersion, confirmed, token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}
