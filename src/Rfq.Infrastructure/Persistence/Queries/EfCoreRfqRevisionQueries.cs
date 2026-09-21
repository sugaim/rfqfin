using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreRfqRevisionQueries(RfqDbContext dbContext) : IRfqRevisionQueries
{
    public async Task<IReadOnlyList<RevisionHistoryItem>> GetAsync(CaseId caseId,
        CancellationToken cancellationToken = default) =>
        await dbContext.RfqRevisions.AsNoTracking().Where(item => item.CaseId == caseId.Value)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new RevisionHistoryItem(new RevisionId(item.RevisionId), item.Status,
                item.Notional, item.SettlementDate, item.SalesAndTradingMessage,
                new StateVersion(item.Version), item.CreatedAt, item.ConfirmedAt,
                item.CopiedFromRevisionId == null ? null : new RevisionId(item.CopiedFromRevisionId.Value),
                item.QuoteSeedRevisionId == null ? null : new RevisionId(item.QuoteSeedRevisionId.Value)))
            .ToListAsync(cancellationToken);
}
