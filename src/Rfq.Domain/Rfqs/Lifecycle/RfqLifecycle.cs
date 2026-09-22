namespace Rfq.Domain;

public abstract record RfqLifecycle
{
    protected RfqLifecycle(RevisionId currentRevisionId) => CurrentRevisionId = currentRevisionId;
    public RevisionId CurrentRevisionId { get; }
}

public sealed record DraftRfq : RfqLifecycle
{
    public DraftRfq(RevisionId currentRevisionId) : base(currentRevisionId) { }
}

public sealed record CancelledRfq : RfqLifecycle
{
    public CancelledRfq(RevisionId currentRevisionId) : base(currentRevisionId) { }
}

public abstract record ClosedRfq : RfqLifecycle
{
    protected ClosedRfq(
        RevisionId currentRevisionId,
        QuoteId closedQuoteId,
        DateOnly closedBusinessDate)
        : base(currentRevisionId)
    {
        if (closedQuoteId.Value == Guid.Empty)
        {
            throw new DomainValidationException("Closed Quote ID is required.");
        }

        ClosedQuoteId = closedQuoteId;
        ClosedBusinessDate = closedBusinessDate;
    }

    public QuoteId ClosedQuoteId { get; }
    public DateOnly ClosedBusinessDate { get; }
}

public sealed record HitRfq : ClosedRfq
{
    public HitRfq(
        RevisionId currentRevisionId,
        QuoteId closedQuoteId,
        DateOnly closedBusinessDate)
        : base(currentRevisionId, closedQuoteId, closedBusinessDate) { }
}

public sealed record AwayRfq : ClosedRfq
{
    public AwayRfq(
        RevisionId currentRevisionId,
        QuoteId closedQuoteId,
        DateOnly closedBusinessDate)
        : base(currentRevisionId, closedQuoteId, closedBusinessDate) { }
}
