using Rfq.Domain;

namespace Rfq.Application;

public sealed class ConfirmAmendments(ConfirmAmendment confirm, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<AmendmentItem> items,
        CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await confirm.ExecuteAsync(item, token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}
