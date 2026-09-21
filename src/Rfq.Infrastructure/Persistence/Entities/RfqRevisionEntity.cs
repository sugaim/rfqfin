using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class RfqRevisionEntity
{
    public Guid RevisionId { get; set; }
    public long CaseId { get; set; }
    public RevisionStatus Status { get; set; }
    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateOnly? SettlementDate { get; set; }
    public DateOnly StandardSettlementDate { get; set; }
    public decimal? Notional { get; set; }
    public string SalesAndTradingMessage { get; set; } = string.Empty;
    public Guid? QuoteSeedRevisionId { get; set; }
    public Guid? CopiedFromRevisionId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }
    public RfqCaseEntity RfqCase { get; set; } = null!;
}

