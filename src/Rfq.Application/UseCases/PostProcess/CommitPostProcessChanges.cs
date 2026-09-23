using Rfq.Domain;

namespace Rfq.Application;

public sealed class CommitPostProcessChanges(
    CloseRfqOperation close,
    CancelRfqOperation cancel,
    CorrectOutcomeOperation correct,
    UpdateMemoOperation memo,
    IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<PostProcessCommitItem> items,
        CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            ApplyAsync,
            unitOfWork,
            cancellationToken);

    private async Task<CaseOperationOutcome> ApplyAsync(
        PostProcessCommitItem item,
        CancellationToken cancellationToken)
    {
        if (item.LifecycleChange is null && item.MemoChange is null)
        {
            throw new RfqRequestValidationException(
                "A lifecycle or memo change is required.");
        }

        if (item.LifecycleChange is not null)
        {
            await ApplyLifecycleAsync(
                item.CaseId,
                item.ExpectedCurrentVersion,
                item.LifecycleChange,
                cancellationToken);
        }

        if (item.MemoChange is not null)
        {
            await memo.ApplyOwnAsync(
                item.CaseId,
                item.MemoChange.Value,
                item.MemoChange.ExpectedVersion,
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CaseOperationOutcome.Applied;
    }

    private Task ApplyLifecycleAsync(
        CaseId caseId,
        StateVersion expectedCurrentVersion,
        PostProcessLifecycleChange change,
        CancellationToken cancellationToken) => change.Type switch
        {
            PostProcessLifecycleChangeKind.Hit => AsTask(close.ApplyAsync(
                caseId,
                RfqStatus.Hit,
                expectedCurrentVersion,
                cancellationToken)),
            PostProcessLifecycleChangeKind.Away => AsTask(close.ApplyAsync(
                caseId,
                RfqStatus.Away,
                expectedCurrentVersion,
                cancellationToken)),
            PostProcessLifecycleChangeKind.Cancel => AsTask(cancel.ApplyAsync(
                caseId,
                expectedCurrentVersion,
                cancellationToken)),
            PostProcessLifecycleChangeKind.CorrectToHit => AsTask(correct.ApplyAsync(
                caseId,
                change.CorrectionReason,
                expectedCurrentVersion,
                RfqStatus.Hit,
                cancellationToken)),
            PostProcessLifecycleChangeKind.CorrectToAway => AsTask(correct.ApplyAsync(
                caseId,
                change.CorrectionReason,
                expectedCurrentVersion,
                RfqStatus.Away,
                cancellationToken)),
            _ => throw new RfqRequestValidationException(
                "Unsupported Post Process lifecycle change."),
        };

    private static async Task AsTask<T>(Task<T> operation) => _ = await operation;
}
