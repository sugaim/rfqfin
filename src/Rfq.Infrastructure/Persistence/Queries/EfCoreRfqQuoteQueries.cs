using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreRfqQuoteQueries(RfqDbContext dbContext) : IRfqQuoteQueries
{
    public async Task<IReadOnlyList<QuoteHistoryItem>> GetAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default) =>
        await (from quote in dbContext.ConfirmedQuotes.AsNoTracking()
               join revision in dbContext.RfqRevisions.AsNoTracking()
                   on quote.RevisionId equals revision.RevisionId
               where revision.CaseId == caseId.Value
               orderby quote.ConfirmedAt descending
               select new QuoteHistoryItem(
                   new QuoteId(quote.QuoteId),
                   new RevisionId(quote.RevisionId),
                   quote.Mode,
                   quote.ConfirmedAt,
                   quote.ExpiresAt,
                   quote.RequestReasonAnswered))
            .ToListAsync(cancellationToken);
}
