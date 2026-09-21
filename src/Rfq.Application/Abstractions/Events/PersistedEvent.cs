namespace Rfq.Application;

public sealed record PersistedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    string Kind,
    string Type,
    string PayloadJson);
