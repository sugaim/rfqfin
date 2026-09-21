using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfUnitOfWork(
    RfqDbContext dbContext,
    PersistedEventSink eventSink) : IUnitOfWork
{
    public EfUnitOfWork(RfqDbContext dbContext) : this(dbContext, new PersistedEventSink()) { }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (eventSink.Pending.Count == 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var cursor = await dbContext.EventCursors
                .FromSqlRaw("SELECT * FROM event_cursors WHERE cursor_key = 'global' FOR UPDATE")
                .SingleAsync(cancellationToken);
            foreach (var pending in eventSink.Pending)
            {
                cursor.LastEventId++;
                var parent = new EventEntity
                {
                    EventId = cursor.LastEventId,
                    OccurredAt = pending.OccurredAt,
                    ActorUserId = pending.ActorUserId,
                };
                dbContext.Events.Add(parent);
                if (pending.Kind == "Rfq")
                {
                    dbContext.RfqEvents.Add(new RfqEventEntity
                    {
                        EventId = parent.EventId,
                        CaseId = pending.CaseId!.Value,
                        Type = pending.Type,
                        PayloadJson = pending.PayloadJson,
                    });
                }
                else
                {
                    dbContext.QuoteEvents.Add(new QuoteEventEntity
                    {
                        EventId = parent.EventId,
                        QuoteId = pending.QuoteId!.Value,
                        Type = pending.Type,
                        PayloadJson = pending.PayloadJson,
                    });
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            eventSink.Clear();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new StateVersionMismatchException(
                "The RFQ was changed by another user. Reload and try again.",
                exception);
        }
    }
}
