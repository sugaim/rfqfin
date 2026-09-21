using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class PostgreSqlQuoteExpiryQueries(RfqDbContext dbContext) : IQuoteExpiryQueries
{
    public async Task<IReadOnlyList<ExpiredQuoteCandidate>> GetExpiredAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        await dbContext.RfqCases.AsNoTracking()
            .Where(item => item.Current.Lifecycle == RfqLifecycleKind.Open
                && item.Current.QuoteStatus == QuoteStatus.Quoted
                && item.Current.CurrentQuoteId != null
                && dbContext.ConfirmedQuotes.Any(quote =>
                    quote.QuoteId == item.Current.CurrentQuoteId
                    && quote.ExpiresAt != null
                    && quote.ExpiresAt <= now))
            .Select(item => new ExpiredQuoteCandidate(
                new CaseId(item.CaseId), new QuoteId(item.Current.CurrentQuoteId.GetValueOrDefault()),
                new StateVersion(item.Current.Version)))
            .ToListAsync(cancellationToken);
}
