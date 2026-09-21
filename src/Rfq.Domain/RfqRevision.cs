namespace Rfq.Domain;

public sealed class RfqRevision
{
    private RfqRevision(
        RevisionId revisionId,
        CaseId caseId,
        RevisionStatus status,
        decimal? notional,
        DateOnly? settlementDate,
        DateOnly standardSettlementDate,
        string salesAndTradingMessage,
        long version,
        DateTimeOffset createdAt,
        UserId createdBy,
        DateTimeOffset? confirmedAt,
        UserId? confirmedBy)
    {
        RevisionId = revisionId;
        CaseId = caseId;
        Status = status;
        Notional = notional;
        SettlementDate = settlementDate;
        StandardSettlementDate = standardSettlementDate;
        SalesAndTradingMessage = salesAndTradingMessage;
        Version = version;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        ConfirmedAt = confirmedAt;
        ConfirmedBy = confirmedBy;
    }

    public RevisionId RevisionId { get; }

    public CaseId CaseId { get; }

    public RevisionStatus Status { get; private set; }

    public decimal? Notional { get; private set; }

    public DateOnly? SettlementDate { get; private set; }

    public DateOnly StandardSettlementDate { get; }

    public string SalesAndTradingMessage { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public UserId CreatedBy { get; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public UserId? ConfirmedBy { get; private set; }

    internal static RfqRevision CreateInitialDraft(
        CaseId caseId,
        decimal? notional,
        DateOnly? settlementDate,
        DateOnly standardSettlementDate,
        string? salesAndTradingMessage,
        DateTimeOffset createdAt,
        UserId createdBy)
    {
        return new RfqRevision(
            RevisionId.New(),
            caseId,
            RevisionStatus.Draft,
            notional,
            settlementDate,
            standardSettlementDate,
            NormalizeMessage(salesAndTradingMessage),
            1,
            createdAt,
            createdBy,
            null,
            null);
    }

    public static RfqRevision Restore(
        RevisionId revisionId,
        CaseId caseId,
        RevisionStatus status,
        decimal? notional,
        DateOnly? settlementDate,
        DateOnly standardSettlementDate,
        string salesAndTradingMessage,
        long version,
        DateTimeOffset createdAt,
        UserId createdBy,
        DateTimeOffset? confirmedAt,
        UserId? confirmedBy)
    {
        return new RfqRevision(
            revisionId,
            caseId,
            status,
            notional,
            settlementDate,
            standardSettlementDate,
            salesAndTradingMessage,
            version,
            createdAt,
            createdBy,
            confirmedAt,
            confirmedBy);
    }

    internal void UpdateDraft(
        decimal? notional,
        DateOnly? settlementDate,
        string? salesAndTradingMessage,
        long expectedVersion)
    {
        EnsureDraft(expectedVersion);
        Notional = notional;
        SettlementDate = settlementDate;
        SalesAndTradingMessage = NormalizeMessage(salesAndTradingMessage);
        Version++;
    }

    internal void Confirm(DateTimeOffset confirmedAt, UserId confirmedBy, long expectedVersion)
    {
        EnsureDraft(expectedVersion);
        Status = RevisionStatus.Confirmed;
        ConfirmedAt = confirmedAt.ToUniversalTime();
        ConfirmedBy = confirmedBy;
        Version++;
    }

    internal void UpdateAndConfirm(
        decimal? notional,
        DateOnly? settlementDate,
        string? salesAndTradingMessage,
        DateTimeOffset confirmedAt,
        UserId confirmedBy,
        long expectedVersion)
    {
        EnsureDraft(expectedVersion);
        Notional = notional;
        SettlementDate = settlementDate;
        SalesAndTradingMessage = NormalizeMessage(salesAndTradingMessage);
        Status = RevisionStatus.Confirmed;
        ConfirmedAt = confirmedAt.ToUniversalTime();
        ConfirmedBy = confirmedBy;
        Version++;
    }

    internal void Discard(long expectedVersion)
    {
        EnsureDraft(expectedVersion);
        Status = RevisionStatus.Discarded;
        Version++;
    }

    private void EnsureDraft(long expectedVersion)
    {
        if (Status != RevisionStatus.Draft)
        {
            throw new InvalidOperationException("Only a Draft Revision can be changed.");
        }

        if (Version != expectedVersion)
        {
            throw new InvalidOperationException("The Draft Revision has changed. Reload and retry.");
        }
    }

    private static string NormalizeMessage(string? value) => value?.Trim() ?? string.Empty;
}
