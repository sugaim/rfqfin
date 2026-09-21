using Rfq.Domain;

namespace Rfq.Application;

public sealed record CalculationFailureRecord(
    Guid FailureLogId,
    long CaseId,
    Guid RevisionId,
    string TraderId,
    Guid RequestId,
    CalculationDriver Driver,
    decimal AttemptedValue,
    decimal SimpleYieldSlide,
    WorkingQuote PriorWorkingQuote,
    string ErrorCode,
    string ErrorMessage,
    DateTimeOffset OccurredAt);
