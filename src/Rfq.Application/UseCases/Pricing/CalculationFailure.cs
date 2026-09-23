using Rfq.Domain;

namespace Rfq.Application;

public sealed class CalculationFailureException(
    Guid failureLogId,
    string code,
    string message) : ExpectedRfqException(RfqErrorKind.CalculationFailure, message)
{
    public Guid FailureLogId { get; } = failureLogId;

    public string Code { get; } = code;
}

public sealed record CalculationFailureRecord(
    Guid FailureLogId,
    CaseId CaseId,
    RevisionId RevisionId,
    UserId TraderId,
    Guid RequestId,
    CalculationDriver Driver,
    decimal AttemptedValue,
    decimal SimpleYieldSlide,
    WorkingQuote PriorWorkingQuote,
    string ErrorCode,
    string ErrorMessage,
    DateTimeOffset OccurredAt);
