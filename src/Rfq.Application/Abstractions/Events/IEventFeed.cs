namespace Rfq.Application;

public interface IEventFeed
{
    Task<IReadOnlyList<EventFeedItem>> GetAfterAsync(
        long eventId,
        CancellationToken cancellationToken = default);

    Task<long> GetLatestIdAsync(CancellationToken cancellationToken = default);
}
