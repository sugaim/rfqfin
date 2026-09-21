using Rfq.Domain;

namespace Rfq.Application;

public sealed class BulkConfirmAmendments(ConfirmAmendment confirm)
{
    public Task<IReadOnlyList<AmendmentItemResult>> ExecuteAsync(
        IReadOnlyList<AmendmentItem> items,
        CancellationToken cancellationToken = default) =>
        AmendmentBulk.ExecuteAsync(items, confirm.ExecuteAsync, "Confirmed", cancellationToken);
}
