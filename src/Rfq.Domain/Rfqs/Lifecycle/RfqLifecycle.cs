namespace Rfq.Domain;

public abstract class RfqLifecycle
{
    protected RfqLifecycle(RevisionId currentRevisionId) => CurrentRevisionId = currentRevisionId;
    public RevisionId CurrentRevisionId { get; }
}

public sealed class DraftRfq(RevisionId currentRevisionId) : RfqLifecycle(currentRevisionId);

public sealed class CancelledRfq(RevisionId currentRevisionId) : RfqLifecycle(currentRevisionId);

public sealed class ClosedRfq : RfqLifecycle
{
    public ClosedRfq(RevisionId currentRevisionId, QuoteId closedQuoteId, RfqStatus outcome)
        : base(currentRevisionId)
    {
        if (outcome is not RfqStatus.Hit and not RfqStatus.Away)
        {
            throw new DomainValidationException("Closed RFQ outcome must be Hit or Away.");
        }

        ClosedQuoteId = closedQuoteId;
        Outcome = outcome;
    }

    public QuoteId ClosedQuoteId { get; }
    public RfqStatus Outcome { get; }
}
