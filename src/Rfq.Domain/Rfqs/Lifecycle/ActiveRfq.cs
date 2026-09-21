namespace Rfq.Domain;

public sealed class ActiveRfq : OpenRfq
{
    public ActiveRfq(
        RevisionId currentRevisionId,
        Ownership ownership,
        ActiveQuoteState quoteState) : base(currentRevisionId, ownership) =>
        QuoteState = quoteState
            ?? throw new DomainValidationException("Quote state is required for an Active RFQ.");

    public ActiveQuoteState QuoteState { get; }
}
