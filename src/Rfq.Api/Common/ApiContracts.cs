using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api;

public enum RfqStatus
{
    Draft,
    Active,
    Presented,
    Cancelled,
    Hit,
    Away,
}

public enum QuoteStatus
{
    Requested,
    Quoted,
}

public enum QuoteRequestReason
{
    Initial,
    Revised,
    Reopened,
    Expired,
    Withdrawn,
}

public enum RevisionStatus
{
    Draft,
    Confirmed,
    Superseded,
    Discarded,
}

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

public sealed class ApiProblemDetails : ProblemDetails
{
    public required string Code { get; init; }

    public required string TraceId { get; init; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public string? CalculationErrorCode { get; init; }

    public Guid? FailureLogId { get; init; }
}

public sealed record CaseOperationResponse(
    long CaseId,
    CaseOperationStatus Status,
    CaseOperationFailureCode? FailureCode,
    string? Message);

public static class CaseOperationApiMapper
{
    public static CaseOperationResponse ToApi(CaseOperationResult value) => new(
        value.CaseId.Value,
        Enum.Parse<CaseOperationStatus>(value.Status.ToString()),
        value.FailureCode is null
            ? null
            : Enum.Parse<CaseOperationFailureCode>(value.FailureCode.Value.ToString()),
        value.Message);
}
