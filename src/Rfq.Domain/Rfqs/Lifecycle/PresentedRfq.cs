namespace Rfq.Domain;

public sealed class PresentedRfq : OpenRfq
{
    public PresentedRfq(
        RevisionId currentRevisionId,
        Ownership ownership,
        QuoteId quoteId) : base(currentRevisionId, ownership) => QuoteId = quoteId;

    public QuoteId QuoteId { get; }
}
