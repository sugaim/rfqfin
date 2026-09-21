namespace Rfq.Domain;

public sealed class RfqCase
{
    private RfqCase(
        CaseId caseId, ClientId clientId, SecurityId securityId,
        CategoryId categorySnapshot, DateTimeOffset createdAt, UserId createdBy,
        UserId? salesId, UserId contactOwnerId, UserId assignedTraderId,
        StateVersion version, RfqRevision currentRevision, RfqLifecycle lifecycle,
        RfqRevision? pendingDraftRevision, CaseId? copiedFromCaseId)
    {
        CaseId = caseId;
        ClientId = clientId;
        SecurityId = securityId;
        CategorySnapshot = categorySnapshot;
        CreatedAt = createdAt.ToUniversalTime();
        CreatedBy = createdBy;
        SalesId = salesId;
        ContactOwnerId = contactOwnerId;
        AssignedTraderId = assignedTraderId;
        Version = version;
        CurrentRevision = currentRevision;
        Lifecycle = lifecycle;
        PendingDraftRevision = pendingDraftRevision;
        CopiedFromCaseId = copiedFromCaseId;
        ValidateInvariant();
    }

    public CaseId CaseId { get; }
    public ClientId ClientId { get; }
    public SecurityId SecurityId { get; }
    public CategoryId CategorySnapshot { get; }
    public DateTimeOffset CreatedAt { get; }
    public UserId CreatedBy { get; }
    public UserId? SalesId { get; }
    public UserId ContactOwnerId { get; }
    public UserId AssignedTraderId { get; }
    public StateVersion Version { get; }
    public RfqRevision CurrentRevision { get; }
    public RfqLifecycle Lifecycle { get; }
    public RfqRevision? PendingDraftRevision { get; }
    public CaseId? CopiedFromCaseId { get; }

    public RfqStatus Status => Lifecycle switch
    {
        DraftRfq => RfqStatus.Draft,
        ActiveRfq => RfqStatus.Active,
        PresentedRfq => RfqStatus.Presented,
        CancelledRfq => RfqStatus.Cancelled,
        HitRfq => RfqStatus.Hit,
        AwayRfq => RfqStatus.Away,
        _ => throw new DomainInvariantException("Unknown RFQ lifecycle.")
    };

    public QuoteStatus? QuoteStatus => Lifecycle switch
    {
        ActiveRfq { QuoteState: QuoteRequested } => Domain.QuoteStatus.Requested,
        ActiveRfq { QuoteState: QuoteConfirmed } or PresentedRfq => Domain.QuoteStatus.Quoted,
        _ => null
    };

    public QuoteRequestReason? QuoteRequestReason =>
        (Lifecycle as ActiveRfq)?.QuoteState is QuoteRequested requested ? requested.Reason : null;
    public QuoteId? CurrentQuoteId => Lifecycle switch
    {
        ActiveRfq { QuoteState: QuoteConfirmed confirmed } => confirmed.QuoteId,
        PresentedRfq presented => presented.QuoteId,
        _ => null
    };
    public QuoteId? ClosedQuoteId => (Lifecycle as ClosedRfq)?.ClosedQuoteId;
    public Ownership? Ownership => (Lifecycle as OpenRfq)?.Ownership;

    public static RfqCase CreateDraft(
        CaseId caseId, RevisionId revisionId, ClientId clientId,
        SecurityId securityId, CategoryId categorySnapshot, UserId assignedTraderId,
        RevisionTerms terms, UserId createdBy, DateTimeOffset createdAt,
        UserId? salesId = null,
        CaseId? copiedFromCaseId = null, RevisionId? copiedFromRevisionId = null)
    {
        var revision = RfqRevision.CreateDraft(
            revisionId, caseId, terms, createdAt, createdBy, copiedFromRevisionId);
        return new RfqCase(
            caseId, clientId, securityId, categorySnapshot, createdAt, createdBy,
            salesId, createdBy, assignedTraderId, new StateVersion(1), revision,
            new DraftRfq(revisionId), null, copiedFromCaseId);
    }

    internal RfqCase Next(
        RfqLifecycle? lifecycle = null,
        RfqRevision? currentRevision = null,
        RfqRevision? pendingDraftRevision = null,
        bool clearPendingDraft = false,
        UserId? assignedTraderId = null,
        UserId? contactOwnerId = null) => new(
            CaseId, ClientId, SecurityId, CategorySnapshot, CreatedAt, CreatedBy,
            SalesId, contactOwnerId ?? ContactOwnerId,
            assignedTraderId ?? AssignedTraderId, Version.Next(),
            currentRevision ?? CurrentRevision, lifecycle ?? Lifecycle,
            clearPendingDraft ? null : pendingDraftRevision ?? PendingDraftRevision,
            CopiedFromCaseId);

    internal static RfqCase Restore(
        CaseId caseId, ClientId clientId, SecurityId securityId,
        CategoryId categorySnapshot, DateTimeOffset createdAt, UserId createdBy,
        UserId? salesId, UserId contactOwnerId, UserId assignedTraderId,
        StateVersion version, RfqRevision currentRevision, RfqLifecycle lifecycle,
        RfqRevision? pendingDraftRevision = null, CaseId? copiedFromCaseId = null) => new(
            caseId, clientId, securityId, categorySnapshot, createdAt, createdBy,
            salesId, contactOwnerId, assignedTraderId, version, currentRevision,
            lifecycle, pendingDraftRevision, copiedFromCaseId);

    internal void EnsureVersion(StateVersion expected) =>
        DomainGuards.EnsureVersion(Version, expected, "RFQ Case");

    private void ValidateInvariant()
    {
        if (Lifecycle.CurrentRevisionId != CurrentRevision.RevisionId)
            throw new DomainInvariantException("Lifecycle and current Revision do not agree.");
        if (Lifecycle is DraftRfq
            && CurrentRevision.Status is not RevisionStatus.Draft and not RevisionStatus.Discarded)
            throw new DomainInvariantException("Draft lifecycle requires a Draft or Discarded Revision.");
        if (Lifecycle is not DraftRfq && CurrentRevision.Status != RevisionStatus.Confirmed)
            throw new DomainInvariantException("Non-Draft lifecycle requires a Confirmed current Revision.");
        if (PendingDraftRevision is not null && PendingDraftRevision.Status != RevisionStatus.Draft)
            throw new DomainInvariantException("Pending amendment must be Draft.");
    }
}
