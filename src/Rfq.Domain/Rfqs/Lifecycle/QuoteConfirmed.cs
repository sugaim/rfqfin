namespace Rfq.Domain;

public sealed class QuoteConfirmed : ActiveQuoteState
{
    public QuoteConfirmed(QuoteId quoteId) => QuoteId = quoteId;
    public QuoteId QuoteId { get; }
}
