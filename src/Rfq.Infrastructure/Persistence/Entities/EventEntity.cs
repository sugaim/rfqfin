using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class EventEntity
{
    public long EventId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? ActorUserId { get; set; }
}

