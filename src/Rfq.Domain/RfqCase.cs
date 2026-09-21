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
        long currentVersion,
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
        CurrentVersion = currentVersion;
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

    public long CurrentVersion { get; private set; }

    public RfqStatus Status => Lifecycle is OpenRfq open
        ? open.Status == OpenRfqStatus.Presented
            ? RfqStatus.Presented
            : RfqStatus.Active
        : RfqStatus.Draft;

    public QuoteStatus? QuoteStatus => (Lifecycle as OpenRfq)?.QuoteStatus;

    public QuoteRequestReason? QuoteRequestReason => (Lifecycle as OpenRfq)?.QuoteRequestReason;

    public QuoteId? CurrentQuoteId => (Lifecycle as OpenRfq)?.CurrentQuoteId;

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
            1,
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
        long currentVersion,
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
            currentVersion,
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
        CurrentVersion++;
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
        CurrentVersion++;
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
        CurrentVersion++;
    }

    public void DiscardInitialDraft(long expectedVersion)
    {
        EnsureInitialDraftLifecycle();
        InitialRevision.Discard(expectedVersion);
        CurrentVersion++;
    }

    public void PickUp(UserId traderId, long expectedVersion)
    {
        EnsureOpenCurrent(expectedVersion);
        if (Owned)
        {
            throw new InvalidOperationException("Owned RFQs cannot be picked up.");
        }

        SetOwnership(traderId, true);
    }

    public void Release(UserId traderId, long expectedVersion)
    {
        EnsureOpenCurrent(expectedVersion);
        if (!Owned || AssignedTraderId != traderId)
        {
            throw new InvalidOperationException("Only the owning Trader can release the RFQ.");
        }

        SetOwnership(AssignedTraderId, false);
    }

    public void AssignTo(UserId traderId, long expectedVersion)
    {
        EnsureOpenCurrent(expectedVersion);
        if (Owned)
        {
            throw new InvalidOperationException("Owned RFQs cannot be assigned.");
        }

        SetOwnership(traderId, false);
    }

    public void TakeOver(UserId traderId, long expectedVersion)
    {
        EnsureOpenCurrent(expectedVersion);
        if (!Owned)
        {
            throw new InvalidOperationException("Unowned RFQs do not require Take Over.");
        }

        if (AssignedTraderId == traderId)
        {
            throw new InvalidOperationException("The RFQ is already owned by this Trader.");
        }

        SetOwnership(traderId, true);
    }

    public void ConfirmQuote(
        QuoteId quoteId,
        RevisionId revisionId,
        long expectedVersion)
    {
        var open = EnsureOpen(expectedVersion);
        if (open.Status != OpenRfqStatus.Active)
        {
            throw new InvalidOperationException("A Presented RFQ cannot confirm a quote.");
        }

        if (open.CurrentRevisionId != revisionId)
        {
            throw new InvalidOperationException(
                "The WorkingQuote does not belong to the current Revision.");
        }

        if (open.QuoteStatus != Domain.QuoteStatus.Requested
            || open.CurrentQuoteId is not null)
        {
            throw new InvalidOperationException(
                "A current ConfirmedQuote is already active.");
        }

        CurrentVersion++;
        Lifecycle = new OpenRfq(
            open.CurrentRevisionId,
            open.ContactOwnerId,
            open.AssignedTraderId,
            Domain.QuoteStatus.Quoted,
            null,
            open.Owned,
            OpenRfqStatus.Active,
            quoteId);
    }

    public void Present(long expectedVersion)
    {
        var open = EnsureOpen(expectedVersion);
        if (open.Status != OpenRfqStatus.Active)
        {
            throw new InvalidOperationException("Only an Active RFQ can be Presented.");
        }

        EnsureCurrentConfirmedQuote(open);
        CurrentVersion++;
        Lifecycle = CopyOpen(open, OpenRfqStatus.Presented);
    }

    public void Unpresent(long expectedVersion)
    {
        var open = EnsureOpen(expectedVersion);
        if (open.Status != OpenRfqStatus.Presented)
        {
            throw new InvalidOperationException("Only a Presented RFQ can be Unpresented.");
        }

        EnsureCurrentConfirmedQuote(open);
        CurrentVersion++;
        Lifecycle = CopyOpen(open, OpenRfqStatus.Active);
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

    private void EnsureOpenCurrent(long expectedVersion)
    {
        _ = EnsureOpen(expectedVersion);
    }

    private void SetOwnership(UserId assignedTraderId, bool owned)
    {
        var open = (OpenRfq)Lifecycle;
        AssignedTraderId = assignedTraderId;
        Owned = owned;
        CurrentVersion++;
        Lifecycle = new OpenRfq(
            open.CurrentRevisionId,
            open.ContactOwnerId,
            assignedTraderId,
            open.QuoteStatus,
            open.QuoteRequestReason,
            owned,
            open.Status,
            open.CurrentQuoteId);
    }

    private OpenRfq EnsureOpen(long expectedVersion)
    {
        if (Lifecycle is not OpenRfq open)
        {
            throw new InvalidOperationException("The RFQ Case is not Open.");
        }

        if (CurrentVersion != expectedVersion)
        {
            throw new InvalidOperationException("The RFQ Case was changed by another user.");
        }

        return open;
    }

    private static void EnsureCurrentConfirmedQuote(OpenRfq open)
    {
        if (open.QuoteStatus != Domain.QuoteStatus.Quoted
            || open.CurrentQuoteId is null)
        {
            throw new InvalidOperationException(
                "Presentation requires a current valid ConfirmedQuote.");
        }
    }

    private static OpenRfq CopyOpen(OpenRfq open, OpenRfqStatus status) => new(
        open.CurrentRevisionId,
        open.ContactOwnerId,
        open.AssignedTraderId,
        open.QuoteStatus,
        open.QuoteRequestReason,
        open.Owned,
        status,
        open.CurrentQuoteId);

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
