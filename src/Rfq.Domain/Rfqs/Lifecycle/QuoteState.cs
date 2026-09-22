namespace Rfq.Domain;

public abstract record ActiveQuoteState;

public sealed record QuoteRequested : ActiveQuoteState
{
    public QuoteRequested(QuoteRequestReason reason) => Reason = reason;
    public QuoteRequestReason Reason { get; }
}

public sealed record QuoteConfirmed : ActiveQuoteState
{
    public QuoteConfirmed(QuoteId quoteId) => QuoteId = quoteId;
    public QuoteId QuoteId { get; }
}

public enum QuoteRequestReason
{
    Initial,
    Revised,
    Reopened,
    Expired,
    Withdrawn
}
