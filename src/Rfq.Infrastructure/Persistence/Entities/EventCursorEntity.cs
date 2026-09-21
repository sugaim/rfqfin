using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class EventCursorEntity
{
    public string CursorKey { get; set; } = string.Empty;
    public long LastEventId { get; set; }
}

