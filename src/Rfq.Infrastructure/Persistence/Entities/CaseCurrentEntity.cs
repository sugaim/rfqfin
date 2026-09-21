using Rfq.Domain;

namespace Rfq.Infrastructure;

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

