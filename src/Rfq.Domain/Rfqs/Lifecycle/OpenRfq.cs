namespace Rfq.Domain;

public abstract class OpenRfq : RfqLifecycle
{
    protected OpenRfq(RevisionId currentRevisionId, Ownership ownership)
        : base(currentRevisionId) => Ownership = ownership
            ?? throw new DomainValidationException("Ownership is required for an Open RFQ.");

    public Ownership Ownership { get; }
}

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

public sealed class PresentedRfq : OpenRfq
{
    public PresentedRfq(
        RevisionId currentRevisionId,
        Ownership ownership,
        QuoteId quoteId) : base(currentRevisionId, ownership) => QuoteId = quoteId;

    public QuoteId QuoteId { get; }
}
