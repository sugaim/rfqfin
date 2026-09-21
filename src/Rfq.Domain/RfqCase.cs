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
        UserId salesId,
        UserId contactOwnerId,
        UserId assignedTraderId,
        bool owned,
        RfqRevision initialRevision,
        RfqLifecycle lifecycle)
    {
        CaseId = caseId;
        ClientId = clientId;
        SecurityId = securityId;
        CategorySnapshot = categorySnapshot;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        SalesId = salesId;
        ContactOwnerId = contactOwnerId;
        AssignedTraderId = assignedTraderId;
        Owned = owned;
        InitialRevision = initialRevision;
        Lifecycle = lifecycle;
    }

    public CaseId CaseId { get; }

    public ClientId ClientId { get; }

    public SecurityId SecurityId { get; }

    public CategoryId CategorySnapshot { get; }

    public DateTimeOffset CreatedAt { get; }

    public UserId CreatedBy { get; }

    public UserId SalesId { get; }

    public UserId ContactOwnerId { get; }

    public UserId AssignedTraderId { get; private set; }

    public bool Owned { get; private set; }

    public RfqStatus Status => Lifecycle is OpenRfq ? RfqStatus.Active : RfqStatus.Draft;

    public QuoteStatus? QuoteStatus => (Lifecycle as OpenRfq)?.QuoteStatus;

    public QuoteRequestReason? QuoteRequestReason => (Lifecycle as OpenRfq)?.QuoteRequestReason;

    public RfqLifecycle Lifecycle { get; private set; }

    public RfqRevision InitialRevision { get; }

    public static RfqCase CreateDraft(
        CaseId caseId,
        ClientId clientId,
        SecurityId securityId,
        CategoryId categorySnapshot,
        UserId assignedTraderId,
        decimal? notional,
        DateOnly? settlementDate,
        DateOnly standardSettlementDate,
        string? salesAndTradingMessage,
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
            notional,
            settlementDate,
            standardSettlementDate,
            salesAndTradingMessage,
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
            createdBy,
            assignedTraderId,
            false,
            initialRevision,
            new DraftRfq(initialRevision.RevisionId));
    }

    public static RfqCase Restore(
        CaseId caseId,
        ClientId clientId,
        SecurityId securityId,
        CategoryId categorySnapshot,
        DateTimeOffset createdAt,
        UserId createdBy,
        UserId salesId,
        UserId contactOwnerId,
        UserId assignedTraderId,
        bool owned,
        RfqRevision initialRevision,
        RfqLifecycle lifecycle)
    {
        return new RfqCase(
            caseId,
            clientId,
            securityId,
            categorySnapshot,
            createdAt,
            createdBy,
            salesId,
            contactOwnerId,
            assignedTraderId,
            owned,
            initialRevision,
            lifecycle);
    }

    public void UpdateInitialDraft(
        decimal? notional,
        DateOnly? settlementDate,
        string? salesAndTradingMessage,
        UserId assignedTraderId,
        long expectedVersion)
    {
        EnsureInitialDraftLifecycle();
        InitialRevision.UpdateDraft(
            notional,
            settlementDate,
            salesAndTradingMessage,
            expectedVersion);
        AssignedTraderId = assignedTraderId;
    }

    public void ConfirmInitial(
        DateOnly systemDate,
        UserId confirmedBy,
        DateTimeOffset confirmedAt,
        long expectedVersion)
    {
        EnsureInitialDraftLifecycle();
        ValidateConfirmation(
            InitialRevision.Notional,
            InitialRevision.SettlementDate,
            systemDate);
        InitialRevision.Confirm(confirmedAt, confirmedBy, expectedVersion);
        OpenInitialRevision();
    }

    public void UpdateAndConfirmInitial(
        decimal? notional,
        DateOnly? settlementDate,
        string? salesAndTradingMessage,
        UserId assignedTraderId,
        DateOnly systemDate,
        UserId confirmedBy,
        DateTimeOffset confirmedAt,
        long expectedVersion)
    {
        EnsureInitialDraftLifecycle();
        ValidateConfirmation(notional, settlementDate, systemDate);
        InitialRevision.UpdateAndConfirm(
            notional,
            settlementDate,
            salesAndTradingMessage,
            confirmedAt,
            confirmedBy,
            expectedVersion);
        AssignedTraderId = assignedTraderId;
        OpenInitialRevision();
    }

    public void DiscardInitialDraft(long expectedVersion)
    {
        EnsureInitialDraftLifecycle();
        InitialRevision.Discard(expectedVersion);
    }

    private void OpenInitialRevision()
    {
        Lifecycle = new OpenRfq(
            InitialRevision.RevisionId,
            ContactOwnerId,
            AssignedTraderId,
            Domain.QuoteStatus.Requested,
            Domain.QuoteRequestReason.Initial,
            false);
        Owned = false;
    }

    private void EnsureInitialDraftLifecycle()
    {
        if (Lifecycle is not DraftRfq)
        {
            throw new InvalidOperationException("The RFQ Case is not an initial Draft.");
        }
    }

    private static void ValidateConfirmation(
        decimal? notional,
        DateOnly? settlementDate,
        DateOnly systemDate)
    {
        if (notional is null or <= 0)
        {
            throw new ArgumentException("Notional must be greater than zero.", nameof(notional));
        }

        if (settlementDate is null)
        {
            throw new ArgumentException("Settlement date is required.", nameof(settlementDate));
        }

        if (settlementDate < systemDate)
        {
            throw new ArgumentException(
                "Settlement date must be on or after the system date.",
                nameof(settlementDate));
        }
    }
}
