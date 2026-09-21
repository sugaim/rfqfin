namespace Rfq.Application;

public sealed record PersistedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    string Kind,
    string Type,
    string PayloadJson);

public interface IEventFeed
{
    Task<IReadOnlyList<PersistedEvent>> GetAfterAsync(
        long eventId,
        CancellationToken cancellationToken = default);

    Task<long> GetLatestIdAsync(CancellationToken cancellationToken = default);
}
