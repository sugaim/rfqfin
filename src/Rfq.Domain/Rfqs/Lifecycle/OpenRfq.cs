namespace Rfq.Domain;

public abstract class OpenRfq : RfqLifecycle
{
    protected OpenRfq(RevisionId currentRevisionId, Ownership ownership)
        : base(currentRevisionId) => Ownership = ownership
            ?? throw new DomainValidationException("Ownership is required for an Open RFQ.");

    public Ownership Ownership { get; }
}
