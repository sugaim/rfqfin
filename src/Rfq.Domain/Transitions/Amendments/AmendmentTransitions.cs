namespace Rfq.Domain;

public static class AmendmentTransitions
{
    public static AmendmentSaveResult StartDraft(
        RfqCase rfq,
        RevisionId newRevisionId,
        UserId editedBy,
        DateTimeOffset editedAt,
        DateOnly draftCreatedBusinessDate,
        StateVersion expectedCaseVersion)
    {
        OpenRfq open = EnsureOpen(rfq, expectedCaseVersion);
        if (rfq.PendingDraftRevision is not null)
        {
            throw new DomainRuleViolationException("The RFQ Case already has a Draft amendment.");
        }

        RfqRevision draft = RfqRevision.CreateDraft(
            newRevisionId,
            rfq.CaseId,
            rfq.CurrentRevision.Terms,
            editedAt,
            draftCreatedBusinessDate,
            editedBy,
            rfq.CurrentRevision.RevisionId,
            rfq.CurrentRevision.RevisionId);
        return new AmendmentSaveResult(rfq.Next(lifecycle: open, pendingDraftRevision: draft), draft);
    }

    public static AmendmentSaveResult SaveDraft(
        RfqCase rfq,
        RevisionId newRevisionId,
        RevisionTerms terms,
        UserId editedBy,
        DateTimeOffset editedAt,
        DateOnly draftCreatedBusinessDate,
        StateVersion expectedCaseVersion,
        StateVersion? expectedDraftVersion = null,
        RevisionId? copiedFromRevisionId = null,
        RevisionId? quoteSeedRevisionId = null)
    {
        OpenRfq open = EnsureOpen(rfq, expectedCaseVersion);
        RfqRevision draft;
        if (rfq.PendingDraftRevision is null)
        {
            draft = RfqRevision.CreateDraft(
                newRevisionId,
                rfq.CaseId,
                terms,
                editedAt,
                draftCreatedBusinessDate,
                editedBy,
                copiedFromRevisionId ?? rfq.CurrentRevision.RevisionId,
                quoteSeedRevisionId ?? rfq.CurrentRevision.RevisionId);
        }
        else
        {
            if (expectedDraftVersion is null)
            {
                throw new DomainValidationException("Expected Draft Revision version is required.");
            }

            draft = rfq.PendingDraftRevision.UpdateDraft(terms, expectedDraftVersion.Value);
        }
        return new AmendmentSaveResult(rfq.Next(lifecycle: open, pendingDraftRevision: draft), draft);
    }

    public static AmendmentConfirmResult Confirm(
        RfqCase rfq,
        DateOnly businessDate,
        UserId confirmedBy,
        DateTimeOffset confirmedAt,
        StateVersion expectedCaseVersion,
        StateVersion expectedDraftVersion)
    {
        OpenRfq open = EnsureOpen(rfq, expectedCaseVersion);
        RfqRevision draft = rfq.PendingDraftRevision
            ?? throw new DomainRuleViolationException("The RFQ Case has no Draft amendment.");
        if (draft.Terms == rfq.CurrentRevision.Terms)
        {
            throw new DomainRuleViolationException(
                "A zero-difference Draft amendment cannot be confirmed.");
        }

        RfqRevision superseded = rfq.CurrentRevision.Supersede();
        RfqRevision confirmed = draft.Confirm(
            draft.Terms, businessDate, confirmedBy, confirmedAt, expectedDraftVersion);
        RfqCase next = rfq.Next(
            lifecycle: new ActiveRfq(
                confirmed.RevisionId,
                open.Ownership,
                new QuoteRequested(QuoteRequestReason.Revised)),
            currentRevision: confirmed,
            clearPendingDraft: true);
        return new AmendmentConfirmResult(next, superseded, confirmed);
    }

    public static AmendmentDiscardResult Discard(
        RfqCase rfq, StateVersion expectedCaseVersion, StateVersion expectedDraftVersion)
    {
        OpenRfq open = EnsureOpen(rfq, expectedCaseVersion);
        RfqRevision draft = rfq.PendingDraftRevision
            ?? throw new DomainRuleViolationException("The RFQ Case has no Draft amendment.");
        RfqRevision discarded = draft.Discard(expectedDraftVersion);
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
