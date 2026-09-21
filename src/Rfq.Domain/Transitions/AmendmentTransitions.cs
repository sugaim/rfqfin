namespace Rfq.Domain;

public static class AmendmentTransitions
{
    public static AmendmentSaveResult SaveDraft(
        RfqCase rfq, RevisionId newRevisionId, RevisionTerms terms,
        UserId editedBy, DateTimeOffset editedAt,
        StateVersion expectedCaseVersion, StateVersion? expectedDraftVersion = null,
        RevisionId? copiedFromRevisionId = null,
        RevisionId? quoteSeedRevisionId = null)
    {
        var open = EnsureOpen(rfq, expectedCaseVersion);
        RfqRevision draft;
        if (rfq.PendingDraftRevision is null)
        {
            draft = RfqRevision.CreateDraft(
                newRevisionId, rfq.CaseId, terms, editedAt, editedBy,
                copiedFromRevisionId ?? rfq.CurrentRevision.RevisionId,
                quoteSeedRevisionId ?? rfq.CurrentRevision.RevisionId);
        }
        else
        {
            if (expectedDraftVersion is null)
                throw new DomainValidationException("Expected Draft Revision version is required.");
            draft = rfq.PendingDraftRevision.UpdateDraft(terms, expectedDraftVersion.Value);
        }
        return new AmendmentSaveResult(rfq.Next(lifecycle: open, pendingDraftRevision: draft), draft);
    }

    public static AmendmentConfirmResult Confirm(
        RfqCase rfq, DateOnly systemDate, UserId confirmedBy,
        DateTimeOffset confirmedAt, StateVersion expectedCaseVersion,
        StateVersion expectedDraftVersion)
    {
        var open = EnsureOpen(rfq, expectedCaseVersion);
        var draft = rfq.PendingDraftRevision
            ?? throw new DomainRuleViolationException("The RFQ Case has no Draft amendment.");
        var superseded = rfq.CurrentRevision.Supersede();
        var confirmed = draft.Confirm(
            draft.Terms, systemDate, confirmedBy, confirmedAt, expectedDraftVersion);
        var next = rfq.Next(
            lifecycle: new ActiveRfq(confirmed.RevisionId, open.Ownership,
                new QuoteRequested(QuoteRequestReason.Revised)),
            currentRevision: confirmed,
            clearPendingDraft: true);
        return new AmendmentConfirmResult(next, superseded, confirmed);
    }

    public static AmendmentDiscardResult Discard(
        RfqCase rfq, StateVersion expectedCaseVersion, StateVersion expectedDraftVersion)
    {
        var open = EnsureOpen(rfq, expectedCaseVersion);
        var draft = rfq.PendingDraftRevision
            ?? throw new DomainRuleViolationException("The RFQ Case has no Draft amendment.");
        var discarded = draft.Discard(expectedDraftVersion);
        return new AmendmentDiscardResult(
            rfq.Next(lifecycle: open, clearPendingDraft: true), discarded);
    }

    private static OpenRfq EnsureOpen(RfqCase rfq, StateVersion expected)
    {
        rfq.EnsureVersion(expected);
        return rfq.Lifecycle as OpenRfq
            ?? throw new DomainRuleViolationException("Amendments require an Open RFQ.");
    }
}
