using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class RfqEventEntity
{
    public long EventId { get; set; }
    public long CaseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public EventEntity Event { get; set; } = null!;
}

