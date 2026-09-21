namespace Rfq.Domain;

public sealed class RfqRevision
{
    private RfqRevision(
        RevisionId revisionId,
        CaseId caseId,
        RevisionStatus status,
        RevisionTerms terms,
        RevisionId? copiedFromRevisionId,
        RevisionId? quoteSeedRevisionId,
        StateVersion version,
        DateTimeOffset createdAt,
        UserId createdBy,
        DateTimeOffset? confirmedAt,
        UserId? confirmedBy)
    {
        RevisionId = revisionId;
        CaseId = caseId;
        Status = status;
        Terms = terms;
        CopiedFromRevisionId = copiedFromRevisionId;
        QuoteSeedRevisionId = quoteSeedRevisionId;
        Version = version;
        CreatedAt = createdAt.ToUniversalTime();
        CreatedBy = createdBy;
        ConfirmedAt = confirmedAt?.ToUniversalTime();
        ConfirmedBy = confirmedBy;
        ValidateInvariant();
    }

    public RevisionId RevisionId { get; }
    public CaseId CaseId { get; }
    public RevisionStatus Status { get; }
    public RevisionTerms Terms { get; }
    public decimal? Notional => Terms.Notional;
    public DateOnly? SettlementDate => Terms.SettlementDate;
    public DateOnly StandardSettlementDate => Terms.StandardSettlementDate;
    public string SalesAndTradingMessage => Terms.SalesAndTradingMessage;
    public RevisionId? CopiedFromRevisionId { get; }
    public RevisionId? QuoteSeedRevisionId { get; }
    public StateVersion Version { get; }
    public DateTimeOffset CreatedAt { get; }
    public UserId CreatedBy { get; }
    public DateTimeOffset? ConfirmedAt { get; }
    public UserId? ConfirmedBy { get; }

    internal static RfqRevision CreateDraft(
        RevisionId revisionId,
        CaseId caseId,
        RevisionTerms terms,
        DateTimeOffset createdAt,
        UserId createdBy,
        RevisionId? copiedFromRevisionId = null,
        RevisionId? quoteSeedRevisionId = null) => new(
            revisionId, caseId, RevisionStatus.Draft, terms,
            copiedFromRevisionId, quoteSeedRevisionId, new StateVersion(1),
            createdAt, createdBy, null, null);

    internal RfqRevision UpdateDraft(RevisionTerms terms, StateVersion expectedVersion)
    {
        EnsureDraft(expectedVersion);
        return Copy(terms: terms, version: Version.Next());
    }

    internal RfqRevision Confirm(
        RevisionTerms terms,
        DateOnly businessDate,
        UserId confirmedBy,
        DateTimeOffset confirmedAt,
        StateVersion expectedVersion)
    {
        EnsureDraft(expectedVersion);
        ValidateConfirmedTerms(terms, businessDate);
        return Copy(
            status: RevisionStatus.Confirmed,
            terms: terms,
            version: Version.Next(),
            confirmedAt: confirmedAt.ToUniversalTime(),
            confirmedBy: confirmedBy);
    }

    internal RfqRevision Discard(StateVersion expectedVersion)
    {
        EnsureDraft(expectedVersion);
        return Copy(status: RevisionStatus.Discarded, version: Version.Next());
    }

    internal RfqRevision Supersede()
    {
        if (Status != RevisionStatus.Confirmed)
            throw new DomainRuleViolationException("Only a Confirmed Revision can be Superseded.");
        return Copy(status: RevisionStatus.Superseded, version: Version.Next());
    }

    internal static RfqRevision Restore(
        RevisionId revisionId, CaseId caseId, RevisionStatus status,
        RevisionTerms terms, RevisionId? copiedFromRevisionId,
        RevisionId? quoteSeedRevisionId, StateVersion version,
        DateTimeOffset createdAt, UserId createdBy,
        DateTimeOffset? confirmedAt, UserId? confirmedBy) => new(
            revisionId, caseId, status, terms, copiedFromRevisionId,
            quoteSeedRevisionId, version, createdAt, createdBy, confirmedAt, confirmedBy);

    private RfqRevision Copy(
        RevisionStatus? status = null,
        RevisionTerms? terms = null,
        StateVersion? version = null,
        DateTimeOffset? confirmedAt = null,
        UserId? confirmedBy = null) => new(
            RevisionId, CaseId, status ?? Status, terms ?? Terms,
            CopiedFromRevisionId, QuoteSeedRevisionId, version ?? Version,
            CreatedAt, CreatedBy, confirmedAt ?? ConfirmedAt, confirmedBy ?? ConfirmedBy);

    private void EnsureDraft(StateVersion expectedVersion)
    {
        if (Status != RevisionStatus.Draft)
            throw new DomainRuleViolationException("Only a Draft Revision can be changed.");
        DomainGuards.EnsureVersion(Version, expectedVersion, "Revision");
    }

    private static void ValidateConfirmedTerms(RevisionTerms terms, DateOnly businessDate)
    {
        if (terms.Notional is null or <= 0)
            throw new DomainValidationException("Notional must be greater than zero.");
        if (terms.SettlementDate is null)
            throw new DomainValidationException("Settlement date is required.");
        if (terms.SettlementDate < businessDate)
            throw new DomainValidationException("Settlement date must be on or after the system date.");
    }

    private void ValidateInvariant()
    {
        if (Status == RevisionStatus.Confirmed && (ConfirmedAt is null || ConfirmedBy is null))
            throw new DomainInvariantException("A Confirmed Revision requires confirmation metadata.");
    }
}
