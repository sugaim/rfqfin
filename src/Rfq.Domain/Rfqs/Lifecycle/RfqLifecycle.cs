namespace Rfq.Domain;

public abstract class RfqLifecycle
{
    protected RfqLifecycle(RevisionId currentRevisionId) => CurrentRevisionId = currentRevisionId;
    public RevisionId CurrentRevisionId { get; }
}
