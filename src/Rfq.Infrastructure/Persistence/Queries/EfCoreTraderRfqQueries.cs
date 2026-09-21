using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreTraderRfqQueries(RfqDbContext dbContext) : ITraderRfqQueries
{
    public async Task<IReadOnlyList<TraderRfqListItem>> GetAsync(
        DeskId deskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deskId);
        var cases = await dbContext.RfqCases.AsNoTracking()
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
        var clientIds = cases.Select(item => item.ClientId).Distinct().ToArray();
        var securityIds = cases.Select(item => item.SecurityId).Distinct().ToArray();
        var revisionIds = cases.Select(item => item.Current.CurrentRevisionId).ToArray();
        var displayQuoteIds = cases.Select(item => item.Current.CurrentQuoteId ?? item.Current.ClosedQuoteId)
            .Where(item => item != null).Select(item => item!.Value).ToArray();
        var clients = await dbContext.Clients.AsNoTracking()
            .Where(item => clientIds.Contains(item.ClientId))
            .ToDictionaryAsync(item => item.ClientId, item => item.Name, cancellationToken);
        var securities = await dbContext.Securities.AsNoTracking()
            .Where(item => securityIds.Contains(item.SecurityId))
            .ToDictionaryAsync(item => item.SecurityId, cancellationToken);
        var quotes = await dbContext.WorkingQuotes.AsNoTracking()
            .Where(item => revisionIds.Contains(item.RevisionId))
            .ToDictionaryAsync(item => item.RevisionId, cancellationToken);
        var confirmedQuotes = await dbContext.ConfirmedQuotes.AsNoTracking()
            .Where(item => displayQuoteIds.Contains(item.QuoteId))
            .ToDictionaryAsync(item => item.QuoteId, cancellationToken);

        return cases.Select(entity =>
        {
            clients.TryGetValue(entity.ClientId, out var clientName);
            securities.TryGetValue(entity.SecurityId, out var security);
            quotes.TryGetValue(entity.Current.CurrentRevisionId, out var quoteEntity);
            var quote = quoteEntity is null
                ? throw new DomainInvariantException(
                    $"WorkingQuote for Revision '{entity.Current.CurrentRevisionId}' was not found.")
                : WorkingQuoteMapper.ToDomain(quoteEntity);
            ConfirmedQuoteEntity? confirmedQuote = null;
            var displayQuoteId = entity.Current.CurrentQuoteId ?? entity.Current.ClosedQuoteId;
            if (displayQuoteId is not null)
                confirmedQuotes.TryGetValue(displayQuoteId.Value, out confirmedQuote);
            return new TraderRfqListItem(
                new CaseId(entity.CaseId), ClientId.Create(entity.ClientId),
                clientName ?? entity.ClientId, SecurityId.Create(entity.SecurityId),
                security?.JapaneseName ?? entity.SecurityId,
                security?.BbgDisplay ?? entity.SecurityId,
                CategoryId.Create(entity.CategorySnapshot), entity.Current.RfqStatus,
                entity.Current.QuoteStatus, entity.Current.QuoteRequestReason,
                new RevisionId(entity.Current.CurrentRevisionId),
                entity.Current.CurrentQuoteId == null ? null : new QuoteId(entity.Current.CurrentQuoteId.Value),
                entity.Current.ClosedQuoteId == null ? null : new QuoteId(entity.Current.ClosedQuoteId.Value),
                confirmedQuote?.ConfirmedAt, confirmedQuote?.ExpiresAt,
                entity.Current.CurrentRevision.QuoteSeedRevisionId == null ? null
                    : new RevisionId(entity.Current.CurrentRevision.QuoteSeedRevisionId.Value),
                UserId.Create(entity.Current.ContactOwnerId),
                UserId.Create(entity.Current.AssignedTraderId), entity.Current.Owned,
                new StateVersion(entity.Current.Version),
                entity.Current.CurrentRevision.SettlementDate,
                entity.Current.CurrentRevision.Notional, quote.Mode, quote.Calculated,
                quote.Manual, quote.Version, entity.TraderMemo.Value,
                new StateVersion(entity.TraderMemo.Version), entity.CreatedAt);
        }).ToArray();
    }
}
