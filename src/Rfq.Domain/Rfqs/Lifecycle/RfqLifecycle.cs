namespace Rfq.Domain;

public abstract class RfqLifecycle
{
    protected RfqLifecycle(RevisionId currentRevisionId) => CurrentRevisionId = currentRevisionId;
    public RevisionId CurrentRevisionId { get; }
}

public enum RfqLifecycleKind { Draft, Open, Cancelled, Closed }
public enum RfqStatus { Draft, Active, Presented, Cancelled, Hit, Away }
public enum QuoteStatus { Requested, Quoted }
public enum QuoteRequestReason { Initial, Revised, Reopened, Expired, Withdrawn }
