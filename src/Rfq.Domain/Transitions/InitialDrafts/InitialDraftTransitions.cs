namespace Rfq.Domain;

public static class InitialDraftTransitions
{
    public static RfqCase Update(
        RfqCase rfq, RevisionTerms terms, UserId assignedTraderId,
        StateVersion expectedRevisionVersion)
    {
        EnsureInitialDraft(rfq);
        var revision = rfq.CurrentRevision.UpdateDraft(terms, expectedRevisionVersion);
        return rfq.Next(currentRevision: revision, assignedTraderId: assignedTraderId);
    }

    public static RfqCase Discard(RfqCase rfq, StateVersion expectedRevisionVersion)
    {
        EnsureInitialDraft(rfq);
        var revision = rfq.CurrentRevision.Discard(expectedRevisionVersion);
        return rfq.Next(currentRevision: revision);
    }

    internal static void EnsureInitialDraft(RfqCase rfq)
    {
        if (rfq.Lifecycle is not DraftRfq)
            throw new DomainRuleViolationException("The RFQ Case is not an initial Draft.");
    }
}
