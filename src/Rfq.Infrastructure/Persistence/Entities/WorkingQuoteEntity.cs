using Rfq.Domain;

namespace Rfq.Infrastructure;

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

