using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreEventFeed(
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
                                    && master.DeskId == user.DeskId.Value))
                        orderby child.EventId
                        select new RfqEventRow(
                            child.EventId,
                            parent.OccurredAt,
                            parent.ActorUserId,
                            child.CaseId,
                            child.Type,
                            child.PayloadJson);
        var quoteEvents = from child in dbContext.QuoteEvents.AsNoTracking()
                          join parent in dbContext.Events.AsNoTracking() on child.EventId equals parent.EventId
                          join quote in dbContext.ConfirmedQuotes.AsNoTracking() on child.QuoteId equals quote.QuoteId
                          join revision in dbContext.RfqRevisions.AsNoTracking() on quote.RevisionId equals revision.RevisionId
                          join rfq in dbContext.RfqCases.AsNoTracking() on revision.CaseId equals rfq.CaseId
                          where child.EventId > eventId
                              && (rfq.SalesId == user.UserId.Value
                                  || rfq.Current.ContactOwnerId == user.UserId.Value
                                  || dbContext.MasterUsers.Any(master =>
                                      master.UserId == rfq.Current.AssignedTraderId
                                      && master.DeskId == user.DeskId.Value))
                          orderby child.EventId
                          select new QuoteEventRow(
                              child.EventId,
                              parent.OccurredAt,
                              parent.ActorUserId,
                              revision.CaseId,
                              child.QuoteId,
                              child.Type,
                              child.PayloadJson);
        var rfqRows = await rfqEvents.Take(1000).ToListAsync(cancellationToken);
        var quoteRows = await quoteEvents.Take(1000).ToListAsync(cancellationToken);
        var rfqItems = rfqRows.Select(row => (PersistedEvent)new PersistedRfqEvent(
            row.EventId,
            row.OccurredAt,
            ToUserId(row.ActorUserId),
            new CaseId(row.CaseId),
            PersistedEventTypeParser.ParseRfq(row.Type),
            row.PayloadJson));
        var quoteItems = quoteRows.Select(row => (PersistedEvent)new PersistedQuoteEvent(
            row.EventId,
            row.OccurredAt,
            ToUserId(row.ActorUserId),
            new CaseId(row.CaseId),
            new QuoteId(row.QuoteId),
            PersistedEventTypeParser.ParseQuote(row.Type),
            row.PayloadJson));
        return rfqItems.Concat(quoteItems)
            .OrderBy(item => item.EventId)
            .Take(1000)
            .ToArray();
    }

    public Task<long> GetLatestIdAsync(CancellationToken cancellationToken = default) =>
        dbContext.Events.AsNoTracking().Select(item => (long?)item.EventId)
            .MaxAsync(cancellationToken).ContinueWith(task => task.Result ?? 0,
                cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private static UserId? ToUserId(string? value) =>
        value is null ? null : UserId.Create(value);

    private sealed record RfqEventRow(
        long EventId,
        DateTimeOffset OccurredAt,
        string? ActorUserId,
        long CaseId,
        string Type,
        string PayloadJson);

    private sealed record QuoteEventRow(
        long EventId,
        DateTimeOffset OccurredAt,
        string? ActorUserId,
        long CaseId,
        Guid QuoteId,
        string Type,
        string PayloadJson);
}

internal static class PersistedEventTypeParser
{
    public static RfqTransitionKind ParseRfq(string value) =>
        Parse<RfqTransitionKind>(value, "RFQ");

    public static QuoteTransitionKind ParseQuote(string value) =>
        Parse<QuoteTransitionKind>(value, "Quote");

    private static TKind Parse<TKind>(string value, string eventKind)
        where TKind : struct, Enum
    {
        if (Enum.TryParse<TKind>(value, ignoreCase: false, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new DomainInvariantException(
            $"Persisted {eventKind} event type '{value}' is invalid.");
    }
}
