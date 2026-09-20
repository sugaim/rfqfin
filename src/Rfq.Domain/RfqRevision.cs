namespace Rfq.Domain;

public sealed class RfqRevision
{
    private RfqRevision(
        RevisionId revisionId,
        CaseId caseId,
        DateTimeOffset createdAt,
        UserId createdBy)
    {
        RevisionId = revisionId;
        CaseId = caseId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public RevisionId RevisionId { get; }

    public CaseId CaseId { get; }

    public RevisionStatus Status => RevisionStatus.Draft;

    public long Version => 1;

    public DateTimeOffset CreatedAt { get; }

    public UserId CreatedBy { get; }

    internal static RfqRevision CreateInitialDraft(
        CaseId caseId,
        DateTimeOffset createdAt,
        UserId createdBy)
    {
        return new RfqRevision(RevisionId.New(), caseId, createdAt, createdBy);
    }
}
