using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class RfqCaseEntity
{
    public long CaseId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string SecurityId { get; set; } = string.Empty;
    public string CategorySnapshot { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? SalesId { get; set; }
    public long? CopiedFromCaseId { get; set; }
    public List<RfqRevisionEntity> Revisions { get; set; } = [];
    public CaseCurrentEntity Current { get; set; } = null!;
    public CaseMemoEntity Memo { get; set; } = null!;
}

internal sealed class CaseCurrentEntity
{
    public long CaseId { get; set; }
    public RfqLifecycleKind Lifecycle { get; set; }
    public RfqStatus RfqStatus { get; set; }
    public QuoteStatus? QuoteStatus { get; set; }
    public QuoteRequestReason? QuoteRequestReason { get; set; }
    public Guid CurrentRevisionId { get; set; }
    public Guid? CurrentQuoteId { get; set; }
    public Guid? ClosedQuoteId { get; set; }
    public long Version { get; set; }
    public string ContactOwnerId { get; set; } = string.Empty;
    public string AssignedTraderId { get; set; } = string.Empty;
    public bool Owned { get; set; }
    public RfqCaseEntity RfqCase { get; set; } = null!;
    public RfqRevisionEntity CurrentRevision { get; set; } = null!;
    public ConfirmedQuoteEntity? CurrentQuote { get; set; }
    public ConfirmedQuoteEntity? ClosedQuote { get; set; }
}

internal sealed class CaseMemoEntity
{
    public long CaseId { get; set; }
    public string SalesMemo { get; set; } = string.Empty;
    public string TraderMemo { get; set; } = string.Empty;
    public long Version { get; set; }
    public RfqCaseEntity RfqCase { get; set; } = null!;
}

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

internal sealed class WorkingQuoteEntity
{
    public Guid RevisionId { get; set; }
    public long Version { get; set; }
    public WorkingQuoteMode Mode { get; set; }
    public string? CalculatedPayloadJson { get; set; }
    public string? ManualPayloadJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public RfqRevisionEntity Revision { get; set; } = null!;
}

internal sealed class ConfirmedQuoteEntity
{
    public Guid QuoteId { get; set; }
    public Guid RevisionId { get; set; }
    public string SecurityId { get; set; } = string.Empty;
    public DateOnly SettlementDate { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
    public DateTimeOffset ConfirmedAt { get; set; }
    public WorkingQuoteMode Mode { get; set; }
    public string? CalculatedPayloadJson { get; set; }
    public string? ManualPayloadJson { get; set; }
    public int? ExpiryMinutes { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public QuoteRequestReason RequestReasonAnswered { get; set; }
    public RfqRevisionEntity Revision { get; set; } = null!;
}

internal sealed class CalculationFailureLogEntity
{
    public Guid FailureLogId { get; set; }
    public long CaseId { get; set; }
    public Guid RevisionId { get; set; }
    public string TraderId { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public CalculationDriver Driver { get; set; }
    public decimal AttemptedValue { get; set; }
    public decimal SimpleYieldSlide { get; set; }
    public string PriorWorkingQuoteJson { get; set; } = "{}";
    public string RequestJson { get; set; } = "{}";
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class EventCursorEntity
{
    public string CursorKey { get; set; } = string.Empty;
    public long LastEventId { get; set; }
}

internal sealed class EventEntity
{
    public long EventId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? ActorUserId { get; set; }
}

internal sealed class RfqEventEntity
{
    public long EventId { get; set; }
    public long CaseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public EventEntity Event { get; set; } = null!;
}

internal sealed class QuoteEventEntity
{
    public long EventId { get; set; }
    public Guid QuoteId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public EventEntity Event { get; set; } = null!;
}

internal sealed class UserGridConfigEntity
{
    public string UserId { get; set; } = string.Empty;
    public string ScreenId { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
