namespace Rfq.Domain;

public sealed class ConfirmedQuote
{
    private ConfirmedQuote(
        QuoteId quoteId,
        RevisionId revisionId,
        SecurityId securityId,
        DateOnly settlementDate,
        UserId confirmedBy,
        DateTimeOffset confirmedAt,
        WorkingQuoteMode mode,
        CalculatedQuotePayload? calculated,
        ManualQuotePayload? manual,
        int? expiryMinutes,
        DateTimeOffset? expiresAt,
        QuoteRequestReason requestReasonAnswered)
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

    public static ConfirmedQuote Create(
        QuoteId quoteId,
        WorkingQuote workingQuote,
        SecurityId securityId,
        DateOnly settlementDate,
        UserId confirmedBy,
        DateTimeOffset confirmedAt,
        int? expiryMinutes,
        QuoteRequestReason requestReasonAnswered)
    {
        if (expiryMinutes is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiryMinutes),
                "Expiry minutes must be positive or null for None.");
        }

        CalculatedQuotePayload? calculated = null;
        ManualQuotePayload? manual = null;
        if (workingQuote.Mode == WorkingQuoteMode.Calculated)
        {
            calculated = workingQuote.Calculated
                ?? throw new InvalidOperationException(
                    "Calculated mode requires a successful calculated payload before Confirm.");
        }
        else
        {
            var values = workingQuote.Manual
                ?? throw new InvalidOperationException(
                    "Manual mode requires Price and Final Simple Yield before Confirm.");
            if (values.Price is null || values.FinalSimpleYield is null)
            {
                throw new InvalidOperationException(
                    "Manual mode requires both Price and Final Simple Yield before Confirm.");
            }

            manual = values;
        }

        var utcConfirmedAt = confirmedAt.ToUniversalTime();
        return new ConfirmedQuote(
            quoteId,
            workingQuote.RevisionId,
            securityId,
            settlementDate,
            confirmedBy,
            utcConfirmedAt,
            workingQuote.Mode,
            calculated,
            manual,
            expiryMinutes,
            expiryMinutes is null ? null : utcConfirmedAt.AddMinutes(expiryMinutes.Value),
            requestReasonAnswered);
    }

    public static ConfirmedQuote Restore(
        QuoteId quoteId,
        RevisionId revisionId,
        SecurityId securityId,
        DateOnly settlementDate,
        UserId confirmedBy,
        DateTimeOffset confirmedAt,
        WorkingQuoteMode mode,
        CalculatedQuotePayload? calculated,
        ManualQuotePayload? manual,
        int? expiryMinutes,
        DateTimeOffset? expiresAt,
        QuoteRequestReason requestReasonAnswered) => new(
        quoteId,
        revisionId,
        securityId,
        settlementDate,
        confirmedBy,
        confirmedAt,
        mode,
        calculated,
        manual,
        expiryMinutes,
        expiresAt,
        requestReasonAnswered);
}
