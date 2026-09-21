namespace Rfq.Domain;

public sealed class QuoteRequested : ActiveQuoteState
{
    public QuoteRequested(QuoteRequestReason reason) => Reason = reason;
    public QuoteRequestReason Reason { get; }
}
