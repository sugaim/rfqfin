namespace Rfq.Domain;

public sealed class RfqCase
{
    private RfqCase(
        CaseId caseId,
        ClientId clientId,
        SecurityId securityId,
        DateTimeOffset createdAt,
        UserId createdBy,
        RfqRevision initialRevision)
    {
        CaseId = caseId;
        ClientId = clientId;
        SecurityId = securityId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        SalesId = createdBy;
        InitialRevision = initialRevision;
        Lifecycle = new DraftRfq(initialRevision.RevisionId);
    }

    public CaseId CaseId { get; }

    public ClientId ClientId { get; }

    public SecurityId SecurityId { get; }

    public DateTimeOffset CreatedAt { get; }

    public UserId CreatedBy { get; }

    public UserId SalesId { get; }

    public RfqStatus Status => RfqStatus.Draft;

    public DraftRfq Lifecycle { get; }

    public RfqRevision InitialRevision { get; }

    public static RfqCase CreateDraft(
        CaseId caseId,
        ClientId clientId,
        SecurityId securityId,
        UserId createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(securityId);
        ArgumentNullException.ThrowIfNull(createdBy);

        var utcCreatedAt = createdAt.ToUniversalTime();
        var initialRevision = RfqRevision.CreateInitialDraft(caseId, utcCreatedAt, createdBy);

        return new RfqCase(
            caseId,
            clientId,
            securityId,
            utcCreatedAt,
            createdBy,
            initialRevision);
    }
}
