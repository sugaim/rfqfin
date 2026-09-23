using Rfq.Domain;

namespace Rfq.Application;

public sealed record DiscardInitialDraftItem(CaseId CaseId, StateVersion ExpectedVersion);

public sealed class ConfirmInitialDrafts(ConfirmInitialDraft confirm, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<UpdateInitialDraftCommand> items, CancellationToken cancellationToken = default) =>
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

public sealed class DiscardInitialDrafts(DiscardInitialDraft discard, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<DiscardInitialDraftItem> items, CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await discard.ExecuteAsync(item.CaseId, item.ExpectedVersion, token);
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}
