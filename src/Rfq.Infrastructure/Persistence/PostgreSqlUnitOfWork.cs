using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class PostgreSqlUnitOfWork(
    RfqDbContext dbContext,
    PersistedEventSink eventSink) : IUnitOfWork
{
    public PostgreSqlUnitOfWork(RfqDbContext dbContext) : this(dbContext, new PersistedEventSink()) { }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (eventSink.Pending.Count == 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            EventCursorEntity cursor = await dbContext.EventCursors
                .FromSqlRaw("SELECT * FROM event_cursors WHERE cursor_key = 'global' FOR UPDATE")
                .SingleAsync(cancellationToken);
            foreach (PendingEvent pending in eventSink.Pending)
            {
                EventPersistenceData persistence = EventPersistenceContract.Serialize(pending);
                cursor.LastEventId++;
                var parent = new EventEntity
                {
                    EventId = cursor.LastEventId,
                    OccurredAt = pending.OccurredAt,
                    ActorUserId = pending.ActorUserId.Value,
                };
                dbContext.Events.Add(parent);
                switch (pending)
                {
                    case PendingRfqEvent rfqEvent:
                        dbContext.RfqEvents.Add(new RfqEventEntity
                        {
                            EventId = parent.EventId,
                            CaseId = rfqEvent.CaseId.Value,
                            Type = persistence.TypeCode,
                            BusinessDate = rfqEvent.BusinessDate,
                            PayloadJson = persistence.PayloadJson,
                        });
                        break;
                    case PendingQuoteEvent quoteEvent:
                        dbContext.QuoteEvents.Add(new QuoteEventEntity
                        {
                            EventId = parent.EventId,
                            QuoteId = quoteEvent.QuoteId.Value,
                            Type = persistence.TypeCode,
                            PayloadJson = persistence.PayloadJson,
                        });
                        break;
                    default:
                        throw new DomainInvariantException(
                            $"Unsupported pending event type '{pending.GetType().Name}'.");
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

    public void DiscardChanges()
    {
        dbContext.ChangeTracker.Clear();
        eventSink.Clear();
    }
}
