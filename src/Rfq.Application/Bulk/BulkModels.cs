using Rfq.Domain;

namespace Rfq.Application;

public enum BulkItemStatus
{
    Succeeded,
    Skipped,
    Failed,
}

public enum BulkFailureCode
{
    VersionConflict,
    InvalidState,
    Validation,
    Forbidden,
    NotFound,
}

public sealed record BulkItemResult(
    CaseId CaseId,
    BulkItemStatus Status,
    BulkFailureCode? Code = null,
    string? Message = null);

internal enum BulkActionOutcome
{
    Succeeded,
    Skipped,
}

internal static class BulkOperation
{
    public static async Task<IReadOnlyList<BulkItemResult>> ExecuteAsync<TItem>(
        IReadOnlyList<TItem> items,
        Func<TItem, CaseId> caseId,
        Func<TItem, CancellationToken, Task<BulkActionOutcome>> action,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var results = new List<BulkItemResult>(items.Count);
        foreach (TItem? item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                BulkActionOutcome outcome = await action(item, cancellationToken);
                results.Add(new BulkItemResult(
                    caseId(item),
                    outcome == BulkActionOutcome.Succeeded
                        ? BulkItemStatus.Succeeded
                        : BulkItemStatus.Skipped));
            }
            catch (ExpectedRfqException exception) when (TryMap(exception.Kind, out BulkFailureCode code))
            {
                unitOfWork.DiscardChanges();
                results.Add(new BulkItemResult(
                    caseId(item), BulkItemStatus.Failed, code, exception.Message));
            }
        }

        return results;
    }

    private static bool TryMap(RfqErrorKind kind, out BulkFailureCode code)
    {
        code = kind switch
        {
            RfqErrorKind.Validation => BulkFailureCode.Validation,
            RfqErrorKind.InvalidState => BulkFailureCode.InvalidState,
            RfqErrorKind.VersionConflict => BulkFailureCode.VersionConflict,
            RfqErrorKind.NotFound => BulkFailureCode.NotFound,
            RfqErrorKind.Forbidden => BulkFailureCode.Forbidden,
            _ => default,
        };
        return kind is RfqErrorKind.Validation
            or RfqErrorKind.InvalidState
            or RfqErrorKind.VersionConflict
            or RfqErrorKind.NotFound
            or RfqErrorKind.Forbidden;
    }
}
