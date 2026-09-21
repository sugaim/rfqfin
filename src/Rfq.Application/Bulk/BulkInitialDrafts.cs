using Rfq.Domain;

namespace Rfq.Application;

public sealed record DiscardInitialDraftItem(CaseId CaseId, StateVersion ExpectedVersion);

public sealed class BulkConfirmInitialDrafts(ConfirmInitialDraft confirm, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<UpdateInitialDraftCommand> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(items, item => item.CaseId, async (item, token) =>
        {
            await confirm.ExecuteAsync(item, token);
            return BulkActionOutcome.Succeeded;
        }, unitOfWork, cancellationToken);
}

public sealed class BulkDiscardInitialDrafts(DiscardInitialDraft discard, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<DiscardInitialDraftItem> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(items, item => item.CaseId, async (item, token) =>
        {
            await discard.ExecuteAsync(item.CaseId, item.ExpectedVersion, token);
            return BulkActionOutcome.Succeeded;
        }, unitOfWork, cancellationToken);
}
