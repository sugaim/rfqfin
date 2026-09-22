using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreTraderRfqQueries(RfqDbContext dbContext) : ITraderRfqQueries
{
    private static readonly string[] StateRfqEvents =
    [
        nameof(RfqTransitionKind.RevisionConfirmed),
        nameof(RfqTransitionKind.Cancelled),
        nameof(RfqTransitionKind.Reopened),
        nameof(RfqTransitionKind.ClosedHit),
        nameof(RfqTransitionKind.ClosedAway),
        nameof(RfqTransitionKind.OutcomeCorrected),
    ];

    private static readonly string[] StateQuoteEvents =
    [
        nameof(QuoteTransitionKind.Confirmed),
        nameof(QuoteTransitionKind.Presented),
        nameof(QuoteTransitionKind.Unpresented),
        nameof(QuoteTransitionKind.Withdrawn),
        nameof(QuoteTransitionKind.Expired),
    ];

    public async Task<IReadOnlyList<TraderRfqListItem>> GetAsync(
        DeskId deskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deskId);
        List<RfqCaseEntity> cases = await dbContext.RfqCases.AsNoTracking()
            .Include(entity => entity.TraderMemo)
            .Include(entity => entity.Current).ThenInclude(current => current.CurrentRevision)
            .Where(entity =>
                (entity.Current.Lifecycle == RfqLifecycleKind.Open
                    || entity.Current.Lifecycle == RfqLifecycleKind.Closed)
                && dbContext.MasterUsers.Any(user =>
                    user.UserId == entity.Current.AssignedTraderId
                    && user.DeskId == deskId.Value))
            .OrderByDescending(entity => entity.CreatedAt).ThenBy(entity => entity.CaseId)
            .ToListAsync(cancellationToken);
        string[] clientIds = [.. cases.Select(item => item.ClientId).Distinct()];
        string[] securityIds = [.. cases.Select(item => item.SecurityId).Distinct()];
        Guid[] revisionIds = [.. cases.Select(item => item.Current.CurrentRevisionId)];
        Guid[] displayQuoteIds = [.. cases.Select(item => item.Current.CurrentQuoteId ?? item.Current.ClosedQuoteId)
            .Where(item => item != null).Select(item => item!.Value)];
        Dictionary<string, string> clients = await dbContext.Clients.AsNoTracking()
            .Where(item => clientIds.Contains(item.ClientId))
            .ToDictionaryAsync(item => item.ClientId, item => item.Name, cancellationToken);
        Dictionary<string, SecurityEntity> securities = await dbContext.Securities.AsNoTracking()
            .Where(item => securityIds.Contains(item.SecurityId))
            .ToDictionaryAsync(item => item.SecurityId, cancellationToken);
        Dictionary<Guid, WorkingQuoteEntity> quotes = await dbContext.WorkingQuotes.AsNoTracking()
            .Where(item => revisionIds.Contains(item.RevisionId))
            .ToDictionaryAsync(item => item.RevisionId, cancellationToken);
        Dictionary<Guid, ConfirmedQuoteEntity> confirmedQuotes = await dbContext.ConfirmedQuotes.AsNoTracking()
            .Where(item => displayQuoteIds.Contains(item.QuoteId))
            .ToDictionaryAsync(item => item.QuoteId, cancellationToken);
        long[] caseIds = [.. cases.Select(item => item.CaseId)];
        List<StateEvent> rfqStateEvents = await dbContext.RfqEvents.AsNoTracking()
            .Where(item => caseIds.Contains(item.CaseId) && StateRfqEvents.Contains(item.Type))
            .Select(item => new StateEvent(item.CaseId, item.Event.OccurredAt))
            .ToListAsync(cancellationToken);
        List<StateEvent> quoteStateEvents = await dbContext.QuoteEvents.AsNoTracking()
            .Where(item => StateQuoteEvents.Contains(item.Type)
                && dbContext.ConfirmedQuotes.Any(quote => quote.QuoteId == item.QuoteId
                    && caseIds.Contains(quote.Revision.CaseId)))
            .Select(item => new StateEvent(
                dbContext.ConfirmedQuotes.Where(quote => quote.QuoteId == item.QuoteId)
                    .Select(quote => quote.Revision.CaseId).Single(),
                item.Event.OccurredAt))
            .ToListAsync(cancellationToken);
        var stateSince = rfqStateEvents.Concat(quoteStateEvents)
            .GroupBy(item => item.CaseId)
            .ToDictionary(group => group.Key, group => group.Max(item => item.OccurredAt));

        return [.. cases.Select(entity =>
        {
            clients.TryGetValue(entity.ClientId, out string? clientName);
            securities.TryGetValue(entity.SecurityId, out SecurityEntity? security);
            quotes.TryGetValue(entity.Current.CurrentRevisionId, out WorkingQuoteEntity? quoteEntity);
            WorkingQuote quote = quoteEntity is null
                ? throw new DomainInvariantException(
                    $"WorkingQuote for Revision '{entity.Current.CurrentRevisionId}' was not found.")
                : WorkingQuoteMapper.ToDomain(quoteEntity);
            ConfirmedQuoteEntity? confirmedQuote = null;
            Guid? displayQuoteId = entity.Current.CurrentQuoteId ?? entity.Current.ClosedQuoteId;
            if (displayQuoteId is not null)
            {
                confirmedQuotes.TryGetValue(displayQuoteId.Value, out confirmedQuote);
            }

            return new TraderRfqListItem(
                new CaseId(entity.CaseId),
                ClientId.Create(entity.ClientId),
                clientName ?? entity.ClientId,
                SecurityId.Create(entity.SecurityId),
                security?.JapaneseName ?? entity.SecurityId,
                security?.BbgDisplay ?? entity.SecurityId,
                CategoryId.Create(entity.CategorySnapshot),
                entity.Current.RfqStatus,
                entity.Current.QuoteStatus,
                entity.Current.QuoteRequestReason,
                new RevisionId(entity.Current.CurrentRevisionId),
                entity.Current.CurrentQuoteId == null ? null : new QuoteId(entity.Current.CurrentQuoteId.Value),
                entity.Current.ClosedQuoteId == null ? null : new QuoteId(entity.Current.ClosedQuoteId.Value),
                confirmedQuote?.ConfirmedAt,
                confirmedQuote?.ExpiresAt,
                entity.Current.CurrentRevision.QuoteSeedRevisionId == null ? null
                    : new RevisionId(entity.Current.CurrentRevision.QuoteSeedRevisionId.Value),
                UserId.Create(entity.Current.ContactOwnerId),
                UserId.Create(entity.Current.AssignedTraderId),
                entity.Current.Owned,
                new StateVersion(entity.Current.Version),
                entity.Current.CurrentRevision.SettlementDate,
                entity.Current.CurrentRevision.Notional,
                entity.Current.CurrentRevision.SalesAndTradingMessage,
                quote.Mode,
                quote.Calculated,
                quote.Manual,
                quote.Version,
                entity.TraderMemo.Value,
                new StateVersion(entity.TraderMemo.Version),
                entity.CreatedAt,
                stateSince.GetValueOrDefault(
                    entity.CaseId,
                    confirmedQuote?.ConfirmedAt
                    ?? entity.Current.CurrentRevision.ConfirmedAt
                    ?? entity.Current.CurrentRevision.CreatedAt));
        })];
    }

    private sealed record StateEvent(long CaseId, DateTimeOffset OccurredAt);
}
