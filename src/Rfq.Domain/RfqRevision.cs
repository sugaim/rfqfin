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
        RevisionId? copiedFromRevisionId,
        RevisionId? quoteSeedRevisionId,
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
        CopiedFromRevisionId = copiedFromRevisionId;
        QuoteSeedRevisionId = quoteSeedRevisionId;
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

    public RevisionId? CopiedFromRevisionId { get; }

    public RevisionId? QuoteSeedRevisionId { get; }

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
        UserId createdBy,
        RevisionId? copiedFromRevisionId = null)
    {
        return new RfqRevision(
            RevisionId.New(),
            caseId,
            RevisionStatus.Draft,
            notional,
            settlementDate,
            standardSettlementDate,
            NormalizeMessage(salesAndTradingMessage),
            copiedFromRevisionId,
            null,
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
        RevisionId? copiedFromRevisionId,
        RevisionId? quoteSeedRevisionId,
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
            copiedFromRevisionId,
            quoteSeedRevisionId,
            version,
            createdAt,
            createdBy,
            confirmedAt,
            confirmedBy);
    }

    internal static RfqRevision CreateAmendment(
        CaseId caseId,
        decimal? notional,
        DateOnly? settlementDate,
        DateOnly standardSettlementDate,
        string? salesAndTradingMessage,
        RevisionId copiedFromRevisionId,
        RevisionId quoteSeedRevisionId,
        DateTimeOffset createdAt,
        UserId createdBy) => new(
            RevisionId.New(),
            caseId,
            RevisionStatus.Draft,
            notional,
            settlementDate,
            standardSettlementDate,
            NormalizeMessage(salesAndTradingMessage),
            copiedFromRevisionId,
            quoteSeedRevisionId,
            1,
            createdAt.ToUniversalTime(),
            createdBy,
            null,
            null);

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

    internal void Supersede()
    {
        if (Status != RevisionStatus.Confirmed)
        {
            throw new InvalidOperationException("Only a Confirmed Revision can be Superseded.");
        }

        Status = RevisionStatus.Superseded;
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
