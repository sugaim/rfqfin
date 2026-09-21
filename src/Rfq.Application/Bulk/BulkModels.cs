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
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var outcome = await action(item, cancellationToken);
                results.Add(new BulkItemResult(caseId(item), outcome == BulkActionOutcome.Succeeded
                    ? BulkItemStatus.Succeeded
                    : BulkItemStatus.Skipped));
            }
            catch (Exception exception) when (TryMap(exception, out var code))
            {
                unitOfWork.DiscardChanges();
                results.Add(new BulkItemResult(
                    caseId(item), BulkItemStatus.Failed, code, exception.Message));
            }
        }

        return results;
    }

    private static bool TryMap(Exception exception, out BulkFailureCode code)
    {
        code = exception switch
        {
            StateVersionMismatchException => BulkFailureCode.VersionConflict,
            DomainRuleViolationException or InvalidOperationException => BulkFailureCode.InvalidState,
            DomainValidationException or ArgumentException => BulkFailureCode.Validation,
            UnauthorizedAccessException => BulkFailureCode.Forbidden,
            KeyNotFoundException => BulkFailureCode.NotFound,
            _ => default,
        };
        return exception is StateVersionMismatchException
            or DomainRuleViolationException
            or InvalidOperationException
            or DomainValidationException
            or ArgumentException
            or UnauthorizedAccessException
            or KeyNotFoundException;
    }
}
