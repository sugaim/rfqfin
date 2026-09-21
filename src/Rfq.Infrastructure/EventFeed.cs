using Microsoft.EntityFrameworkCore;
using Rfq.Application;

namespace Rfq.Infrastructure;

public sealed class PostgreSqlEventFeed(
    RfqDbContext dbContext,
    ICurrentUser currentUser) : IEventFeed
{
    public async Task<IReadOnlyList<PersistedEvent>> GetAfterAsync(
        long eventId,
        CancellationToken cancellationToken = default)
    {
        var user = currentUser.User;
        var rfqEvents = from child in dbContext.RfqEvents.AsNoTracking()
                        join parent in dbContext.Events.AsNoTracking() on child.EventId equals parent.EventId
                        join rfq in dbContext.RfqCases.AsNoTracking() on child.CaseId equals rfq.CaseId
                        where child.EventId > eventId
                            && (rfq.SalesId == user.UserId.Value
                                || rfq.Current.ContactOwnerId == user.UserId.Value
                                || dbContext.MasterUsers.Any(master =>
                                    master.UserId == rfq.Current.AssignedTraderId
                                    && master.DeskId == user.DeskId))
                        orderby child.EventId
                        select new PersistedEvent(child.EventId, parent.OccurredAt,
                            parent.ActorUserId, child.CaseId, "Rfq", child.Type, child.PayloadJson);
        var quoteEvents = from child in dbContext.QuoteEvents.AsNoTracking()
                          join parent in dbContext.Events.AsNoTracking() on child.EventId equals parent.EventId
                          join rfq in dbContext.RfqCases.AsNoTracking() on child.CaseId equals rfq.CaseId
                          where child.EventId > eventId
                              && (rfq.SalesId == user.UserId.Value
                                  || rfq.Current.ContactOwnerId == user.UserId.Value
                                  || dbContext.MasterUsers.Any(master =>
                                      master.UserId == rfq.Current.AssignedTraderId
                                      && master.DeskId == user.DeskId))
                          orderby child.EventId
                          select new PersistedEvent(child.EventId, parent.OccurredAt,
                              parent.ActorUserId, child.CaseId, "Quote", child.Type, child.PayloadJson);
        var rfqItems = await rfqEvents.Take(1000).ToListAsync(cancellationToken);
        var quoteItems = await quoteEvents.Take(1000).ToListAsync(cancellationToken);
        return rfqItems.Concat(quoteItems)
            .OrderBy(item => item.EventId)
            .Take(1000)
            .ToArray();
    }

    public Task<long> GetLatestIdAsync(CancellationToken cancellationToken = default) =>
        dbContext.Events.AsNoTracking().Select(item => (long?)item.EventId)
            .MaxAsync(cancellationToken).ContinueWith(task => task.Result ?? 0,
                cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
