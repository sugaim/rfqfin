namespace Rfq.Domain;

public static class RfqLifecycleTransitions
{
    public static RfqCase ConfirmInitial(
        RfqCase rfq, RevisionTerms terms, UserId assignedTraderId,
        DateOnly businessDate, UserId confirmedBy, DateTimeOffset confirmedAt,
        StateVersion expectedRevisionVersion)
    {
        InitialDraftTransitions.EnsureInitialDraft(rfq);
        var revision = rfq.CurrentRevision.Confirm(
            terms, businessDate, confirmedBy, confirmedAt, expectedRevisionVersion);
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

    public static CloseTransitionResult CloseHit(
        RfqCase rfq, StateVersion expectedVersion) =>
        Close(rfq, expectedVersion, (revisionId, quoteId) => new HitRfq(revisionId, quoteId));

    public static CloseTransitionResult CloseAway(
        RfqCase rfq, StateVersion expectedVersion) =>
        Close(rfq, expectedVersion, (revisionId, quoteId) => new AwayRfq(revisionId, quoteId));

    public static RfqCase CorrectToHit(RfqCase rfq, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (rfq.Lifecycle is not AwayRfq away)
            throw new DomainRuleViolationException("Only an Away RFQ can be corrected to Hit.");
        return rfq.Next(lifecycle: new HitRfq(
            away.CurrentRevisionId, away.ClosedQuoteId));
    }

    public static RfqCase CorrectToAway(RfqCase rfq, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (rfq.Lifecycle is not HitRfq hit)
            throw new DomainRuleViolationException("Only a Hit RFQ can be corrected to Away.");
        return rfq.Next(lifecycle: new AwayRfq(
            hit.CurrentRevisionId, hit.ClosedQuoteId));
    }

    private static CloseTransitionResult Close(
        RfqCase rfq,
        StateVersion expectedVersion,
        Func<RevisionId, QuoteId, ClosedRfq> createClosed)
    {
        rfq.EnsureVersion(expectedVersion);
        var quoteId = rfq.Lifecycle switch
        {
            ActiveRfq { QuoteState: QuoteConfirmed confirmed } => confirmed.QuoteId,
            PresentedRfq presented => presented.QuoteId,
            _ => throw new DomainRuleViolationException(
                "Close requires a current confirmed quote.")
        };
        var discarded = rfq.PendingDraftRevision?.Discard(rfq.PendingDraftRevision.Version);
        var next = rfq.Next(
            lifecycle: createClosed(rfq.CurrentRevision.RevisionId, quoteId),
            clearPendingDraft: true);
        return new CloseTransitionResult(next, discarded);
    }
}
