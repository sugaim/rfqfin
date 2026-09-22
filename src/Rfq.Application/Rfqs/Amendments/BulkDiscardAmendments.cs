using Rfq.Domain;

namespace Rfq.Application;

public sealed class BulkDiscardAmendments(DiscardAmendment discard, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<AmendmentItem> items,
        CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await discard.ExecuteAsync(item, token);
                return BulkActionOutcome.Succeeded;
            },
            unitOfWork,
            cancellationToken);
}
