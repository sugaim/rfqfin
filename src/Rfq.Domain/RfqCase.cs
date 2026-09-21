namespace Rfq.Domain;

public sealed class RfqCase
{
    private RfqCase(
        CaseId caseId,
        ClientId clientId,
        SecurityId securityId,
        CategoryId categorySnapshot,
        DateTimeOffset createdAt,
        UserId createdBy,
        UserId contactOwnerId,
        UserId assignedTraderId,
        RfqRevision initialRevision)
    {
        CaseId = caseId;
        ClientId = clientId;
        SecurityId = securityId;
        CategorySnapshot = categorySnapshot;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        SalesId = createdBy;
        ContactOwnerId = contactOwnerId;
        AssignedTraderId = assignedTraderId;
        InitialRevision = initialRevision;
        Lifecycle = new DraftRfq(initialRevision.RevisionId);
    }

    public CaseId CaseId { get; }

    public ClientId ClientId { get; }

    public SecurityId SecurityId { get; }

    public CategoryId CategorySnapshot { get; }

    public DateTimeOffset CreatedAt { get; }

    public UserId CreatedBy { get; }

    public UserId SalesId { get; }

    public UserId ContactOwnerId { get; }

    public UserId AssignedTraderId { get; }

    public bool Owned => false;

    public RfqStatus Status => RfqStatus.Draft;

    public DraftRfq Lifecycle { get; }

    public RfqRevision InitialRevision { get; }

    public static RfqCase CreateDraft(
        CaseId caseId,
        ClientId clientId,
        SecurityId securityId,
        CategoryId categorySnapshot,
        UserId assignedTraderId,
        DateOnly settlementDate,
        DateOnly standardSettlementDate,
        UserId createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(securityId);
        ArgumentNullException.ThrowIfNull(categorySnapshot);
        ArgumentNullException.ThrowIfNull(assignedTraderId);
        ArgumentNullException.ThrowIfNull(createdBy);

        var utcCreatedAt = createdAt.ToUniversalTime();
        var initialRevision = RfqRevision.CreateInitialDraft(
            caseId,
            settlementDate,
            standardSettlementDate,
            utcCreatedAt,
            createdBy);

        return new RfqCase(
            caseId,
            clientId,
            securityId,
            categorySnapshot,
            utcCreatedAt,
            createdBy,
            createdBy,
            assignedTraderId,
            initialRevision);
    }
}
