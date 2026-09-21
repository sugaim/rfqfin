namespace Rfq.Domain;

public sealed record CloseTransitionResult(RfqCase Rfq, RfqRevision? DiscardedRevision);

public static class RfqLifecycleTransitions
{
    public static RfqCase ConfirmInitial(
        RfqCase rfq, RevisionTerms terms, UserId assignedTraderId,
        DateOnly systemDate, UserId confirmedBy, DateTimeOffset confirmedAt,
        StateVersion expectedRevisionVersion)
    {
        InitialDraftTransitions.EnsureInitialDraft(rfq);
        var revision = rfq.CurrentRevision.Confirm(
            terms, systemDate, confirmedBy, confirmedAt, expectedRevisionVersion);
        return rfq.Next(
            currentRevision: revision,
            assignedTraderId: assignedTraderId,
            lifecycle: new ActiveRfq(revision.RevisionId, new Unowned(),
                new QuoteRequested(QuoteRequestReason.Initial)));
    }

    public static RfqCase Present(RfqCase rfq, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (rfq.Lifecycle is not ActiveRfq { QuoteState: QuoteConfirmed confirmed } active)
            throw new DomainRuleViolationException(
                "Only an Active RFQ with a confirmed quote can be Presented.");
        return rfq.Next(lifecycle: new PresentedRfq(
            active.CurrentRevisionId, active.Ownership, confirmed.QuoteId));
    }

    public static RfqCase Unpresent(RfqCase rfq, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (rfq.Lifecycle is not PresentedRfq presented)
            throw new DomainRuleViolationException("Only a Presented RFQ can be Unpresented.");
        return rfq.Next(lifecycle: new ActiveRfq(
            presented.CurrentRevisionId, presented.Ownership,
            new QuoteConfirmed(presented.QuoteId)));
    }

    public static RfqCase Cancel(RfqCase rfq, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (rfq.Lifecycle is not OpenRfq open)
            throw new DomainRuleViolationException("Only an Open RFQ can be Cancelled.");
        return rfq.Next(lifecycle: new CancelledRfq(open.CurrentRevisionId));
    }

    public static RfqCase Reopen(RfqCase rfq, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (rfq.Lifecycle is not CancelledRfq cancelled)
            throw new DomainRuleViolationException("Only a Cancelled RFQ can be reopened.");
        return rfq.Next(lifecycle: new ActiveRfq(
            cancelled.CurrentRevisionId, new Unowned(),
            new QuoteRequested(QuoteRequestReason.Reopened)));
    }

    public static CloseTransitionResult Close(
        RfqCase rfq, RfqStatus outcome, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (outcome is not RfqStatus.Hit and not RfqStatus.Away)
            throw new DomainValidationException("Close outcome must be Hit or Away.");
        var quoteId = rfq.Lifecycle switch
        {
            ActiveRfq { QuoteState: QuoteConfirmed confirmed } => confirmed.QuoteId,
            PresentedRfq presented => presented.QuoteId,
            _ => throw new DomainRuleViolationException(
                "Close requires a current confirmed quote.")
        };
        var discarded = rfq.PendingDraftRevision?.Discard(rfq.PendingDraftRevision.Version);
        var next = rfq.Next(
            lifecycle: new ClosedRfq(rfq.CurrentRevision.RevisionId, quoteId, outcome),
            clearPendingDraft: true);
        return new CloseTransitionResult(next, discarded);
    }

    public static RfqCase CorrectOutcome(
        RfqCase rfq, RfqStatus outcome, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (rfq.Lifecycle is not ClosedRfq closed)
            throw new DomainRuleViolationException("Only a Closed RFQ outcome can be corrected.");
        if (outcome is not RfqStatus.Hit and not RfqStatus.Away)
            throw new DomainValidationException("Corrected outcome must be Hit or Away.");
        if (closed.Outcome == outcome)
            throw new DomainRuleViolationException("Outcome correction must change Hit to Away or Away to Hit.");
        return rfq.Next(lifecycle: new ClosedRfq(
            closed.CurrentRevisionId, closed.ClosedQuoteId, outcome));
    }
}
