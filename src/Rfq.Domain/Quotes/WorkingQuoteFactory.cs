namespace Rfq.Domain;

public static class WorkingQuoteFactory
{
    public static WorkingQuote CreateInitialFor(
        RfqCase rfq, UserId createdBy, DateTimeOffset createdAt)
    {
        if (rfq.Lifecycle is not OpenRfq
            || rfq.CurrentRevision.Status != RevisionStatus.Confirmed)
            throw new DomainRuleViolationException(
                "A WorkingQuote requires an Open RFQ with a confirmed current Revision.");
        return Empty(rfq.CurrentRevision.RevisionId, createdBy, createdAt);
    }

    public static WorkingQuote CreateForAmendment(
        RfqCase rfq, WorkingQuote? seed, UserId createdBy, DateTimeOffset createdAt)
    {
        if (rfq.Lifecycle is not ActiveRfq { QuoteState: QuoteRequested { Reason: QuoteRequestReason.Revised } })
            throw new DomainRuleViolationException(
                "An amendment WorkingQuote requires an Active Revised quote request.");
        return seed is null
            ? Empty(rfq.CurrentRevision.RevisionId, createdBy, createdAt)
            : new WorkingQuote(rfq.CurrentRevision.RevisionId, seed.Mode, seed.Calculated,
                seed.Manual, new StateVersion(1), createdAt, createdBy, createdAt, createdBy);
    }

    private static WorkingQuote Empty(RevisionId revisionId, UserId by, DateTimeOffset at) =>
        new(revisionId, WorkingQuoteMode.Calculated, null, null,
            new StateVersion(1), at, by, at, by);
}
