namespace Rfq.Domain;

public abstract class ActiveQuoteState;

public sealed class QuoteRequested : ActiveQuoteState
{
    public QuoteRequested(QuoteRequestReason reason) => Reason = reason;
    public QuoteRequestReason Reason { get; }
}

public sealed class QuoteConfirmed : ActiveQuoteState
{
    public QuoteConfirmed(QuoteId quoteId) => QuoteId = quoteId;
    public QuoteId QuoteId { get; }
}

public enum QuoteRequestReason { Initial, Revised, Reopened, Expired, Withdrawn }
