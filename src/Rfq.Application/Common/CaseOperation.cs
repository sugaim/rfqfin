using Rfq.Domain;

namespace Rfq.Application;

public enum CaseOperationStatus
{
    Applied,
    NoChange,
    Failed,
}

public enum CaseOperationFailureCode
{
    VersionConflict,
    InvalidState,
    Validation,
    Forbidden,
    NotFound,
}

public sealed record CaseOperationResult(
    CaseId CaseId,
    CaseOperationStatus Status,
    CaseOperationFailureCode? FailureCode = null,
    string? Message = null);

internal enum CaseOperationOutcome
{
    Applied,
    NoChange,
}

internal static class CaseOperation
{
    public static async Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync<TItem>(
        IReadOnlyList<TItem> items,
        Func<TItem, CaseId> caseId,
        Func<TItem, CancellationToken, Task<CaseOperationOutcome>> action,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var results = new List<CaseOperationResult>(items.Count);
        foreach (TItem? item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                CaseOperationOutcome outcome = await action(item, cancellationToken);
                results.Add(new CaseOperationResult(
                    caseId(item),
                    outcome == CaseOperationOutcome.Applied
                        ? CaseOperationStatus.Applied
                        : CaseOperationStatus.NoChange));
            }
            catch (ExpectedRfqException exception) when (TryMap(exception.Kind, out CaseOperationFailureCode code))
            {
                unitOfWork.DiscardChanges();
                results.Add(new CaseOperationResult(
                    caseId(item), CaseOperationStatus.Failed, code, exception.Message));
            }
        }

        return results;
    }

    private static bool TryMap(RfqErrorKind kind, out CaseOperationFailureCode code)
    {
        code = kind switch
        {
            RfqErrorKind.Validation => CaseOperationFailureCode.Validation,
            RfqErrorKind.InvalidState => CaseOperationFailureCode.InvalidState,
            RfqErrorKind.VersionConflict => CaseOperationFailureCode.VersionConflict,
            RfqErrorKind.NotFound => CaseOperationFailureCode.NotFound,
            RfqErrorKind.Forbidden => CaseOperationFailureCode.Forbidden,
            _ => default,
        };
        return kind is RfqErrorKind.Validation
            or RfqErrorKind.InvalidState
            or RfqErrorKind.VersionConflict
            or RfqErrorKind.NotFound
            or RfqErrorKind.Forbidden;
    }
}
