using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreRfqHistoryQueries(RfqDbContext dbContext) : IRfqHistoryQueries
{
    public async Task<IReadOnlyList<RevisionHistoryItem>> GetRevisionHistoryAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default) =>
        await dbContext.RfqRevisions.AsNoTracking().Where(item => item.CaseId == caseId.Value)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new RevisionHistoryItem(new RevisionId(item.RevisionId), item.Status,
                item.Notional, item.SettlementDate, item.SalesAndTradingMessage, new StateVersion(item.Version),
                item.CreatedAt, item.ConfirmedAt,
                item.CopiedFromRevisionId == null ? null : new RevisionId(item.CopiedFromRevisionId.Value),
                item.QuoteSeedRevisionId == null ? null : new RevisionId(item.QuoteSeedRevisionId.Value)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QuoteHistoryItem>> GetQuoteHistoryAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default) =>
        await (from quote in dbContext.ConfirmedQuotes.AsNoTracking()
               join revision in dbContext.RfqRevisions.AsNoTracking()
                   on quote.RevisionId equals revision.RevisionId
               where revision.CaseId == caseId.Value
               orderby quote.ConfirmedAt descending
               select new QuoteHistoryItem(new QuoteId(quote.QuoteId), new RevisionId(quote.RevisionId), quote.Mode,
                   quote.ConfirmedAt, quote.ExpiresAt, quote.RequestReasonAnswered))
            .ToListAsync(cancellationToken);
}
