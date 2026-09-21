using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class CalculationFailureLogEntity
{
    public Guid FailureLogId { get; set; }
    public long CaseId { get; set; }
    public Guid RevisionId { get; set; }
    public string TraderId { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public CalculationDriver Driver { get; set; }
    public decimal AttemptedValue { get; set; }
    public decimal SimpleYieldSlide { get; set; }
    public string PriorWorkingQuoteJson { get; set; } = "{}";
    public string RequestJson { get; set; } = "{}";
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

