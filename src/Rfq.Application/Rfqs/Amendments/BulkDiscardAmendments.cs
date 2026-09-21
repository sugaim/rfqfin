using Rfq.Domain;

namespace Rfq.Application;

public sealed class BulkDiscardAmendments(DiscardAmendment discard)
{
    public Task<IReadOnlyList<AmendmentItemResult>> ExecuteAsync(
        IReadOnlyList<AmendmentItem> items,
        CancellationToken cancellationToken = default) =>
        AmendmentBulk.ExecuteAsync(items, discard.ExecuteAsync, "Discarded", cancellationToken);
}
