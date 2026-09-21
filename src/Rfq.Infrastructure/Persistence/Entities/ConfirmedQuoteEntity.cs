using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class ConfirmedQuoteEntity
{
    public Guid QuoteId { get; set; }
    public Guid RevisionId { get; set; }
    public string SecurityId { get; set; } = string.Empty;
    public DateOnly SettlementDate { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
    public DateTimeOffset ConfirmedAt { get; set; }
    public WorkingQuoteMode Mode { get; set; }
    public string? CalculatedPayloadJson { get; set; }
    public string? ManualPayloadJson { get; set; }
    public int? ExpiryMinutes { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public QuoteRequestReason RequestReasonAnswered { get; set; }
    public RfqRevisionEntity Revision { get; set; } = null!;
}

