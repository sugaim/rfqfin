using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class QuoteEventEntity
{
    public long EventId { get; set; }
    public Guid QuoteId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public EventEntity Event { get; set; } = null!;
}

