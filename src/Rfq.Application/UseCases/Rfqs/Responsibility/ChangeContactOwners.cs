using Rfq.Domain;

namespace Rfq.Application;

public sealed class ChangeContactOwners(
    ChangeContactOwner changeContactOwner,
    IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        UserId targetContactOwnerId,
        bool confirmed,
        IReadOnlyList<OwnershipItem> items,
        CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await changeContactOwner.ExecuteAsync(
                    item.CaseId,
                    targetContactOwnerId,
                    item.ExpectedCurrentVersion,
                    confirmed,
                    token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}
