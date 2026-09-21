namespace Rfq.Domain;

public sealed class ConfirmedQuote
{
    internal ConfirmedQuote(
        QuoteId quoteId, RevisionId revisionId, SecurityId securityId,
        DateOnly settlementDate, UserId confirmedBy, DateTimeOffset confirmedAt,
        WorkingQuoteMode mode, CalculatedQuotePayload? calculated,
        ManualQuotePayload? manual, int? expiryMinutes,
        DateTimeOffset? expiresAt, QuoteRequestReason requestReasonAnswered)
    {
        QuoteId = quoteId;
        RevisionId = revisionId;
        SecurityId = securityId;
        SettlementDate = settlementDate;
        ConfirmedBy = confirmedBy;
        ConfirmedAt = confirmedAt.ToUniversalTime();
        Mode = mode;
        Calculated = calculated;
        Manual = manual;
        ExpiryMinutes = expiryMinutes;
        ExpiresAt = expiresAt?.ToUniversalTime();
        RequestReasonAnswered = requestReasonAnswered;
    }

    public QuoteId QuoteId { get; }
    public RevisionId RevisionId { get; }
    public SecurityId SecurityId { get; }
    public DateOnly SettlementDate { get; }
    public UserId ConfirmedBy { get; }
    public DateTimeOffset ConfirmedAt { get; }
    public WorkingQuoteMode Mode { get; }
    public CalculatedQuotePayload? Calculated { get; }
    public ManualQuotePayload? Manual { get; }
    public int? ExpiryMinutes { get; }
    public DateTimeOffset? ExpiresAt { get; }
    public QuoteRequestReason RequestReasonAnswered { get; }

    internal static ConfirmedQuote Restore(
        QuoteId quoteId, RevisionId revisionId, SecurityId securityId,
        DateOnly settlementDate, UserId confirmedBy, DateTimeOffset confirmedAt,
        WorkingQuoteMode mode, CalculatedQuotePayload? calculated,
        ManualQuotePayload? manual, int? expiryMinutes,
        DateTimeOffset? expiresAt, QuoteRequestReason requestReasonAnswered) => new(
            quoteId, revisionId, securityId, settlementDate, confirmedBy, confirmedAt,
            mode, calculated, manual, expiryMinutes, expiresAt, requestReasonAnswered);
}
