namespace Rfq.Domain;

public sealed class RfqRevision
{
    private RfqRevision(
        RevisionId revisionId,
        CaseId caseId,
        DateOnly settlementDate,
        DateOnly standardSettlementDate,
        DateTimeOffset createdAt,
        UserId createdBy)
    {
        RevisionId = revisionId;
        CaseId = caseId;
        SettlementDate = settlementDate;
        StandardSettlementDate = standardSettlementDate;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public RevisionId RevisionId { get; }

    public CaseId CaseId { get; }

    public RevisionStatus Status => RevisionStatus.Draft;

    public DateOnly SettlementDate { get; }

    public DateOnly StandardSettlementDate { get; }

    public long Version => 1;

    public DateTimeOffset CreatedAt { get; }

    public UserId CreatedBy { get; }

    internal static RfqRevision CreateInitialDraft(
        CaseId caseId,
        DateOnly settlementDate,
        DateOnly standardSettlementDate,
        DateTimeOffset createdAt,
        UserId createdBy)
    {
        return new RfqRevision(
            RevisionId.New(),
            caseId,
            settlementDate,
            standardSettlementDate,
            createdAt,
            createdBy);
    }
}
