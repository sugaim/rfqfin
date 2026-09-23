using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class BusinessDateRfqSnapshotLoader(RfqDbContext dbContext)
{
    public async Task<BusinessDateRfqSnapshot> LoadAsync(
        DateOnly businessDate,
        long generation,
        CancellationToken cancellationToken = default)
    {
        var salesLoader = new EfCoreSalesRfqQueries(dbContext);
        var traderLoader = new EfCoreTraderRfqQueries(dbContext);
        IReadOnlyList<SalesRfqListItem> sales = await salesLoader.LoadAllAsync(
            businessDate, cancellationToken);
        IReadOnlyList<TraderRfqListItem> trader = await traderLoader.LoadAllAsync(
            businessDate, cancellationToken);
        Dictionary<string, string> deskByTrader = await dbContext.MasterUsers
            .AsNoTracking()
            .ToDictionaryAsync(user => user.UserId, user => user.DeskId, cancellationToken);

        var salesByCase = sales.ToDictionary(item => item.CaseId);
        var traderByCase = trader.ToDictionary(item => item.CaseId);
        ImmutableDictionary<CaseId, RfqSnapshotRoute> routes = salesByCase.Keys
            .Union(traderByCase.Keys)
            .ToImmutableDictionary(
                caseId => caseId,
                caseId =>
                {
                    salesByCase.TryGetValue(caseId, out SalesRfqListItem? salesItem);
                    traderByCase.TryGetValue(caseId, out TraderRfqListItem? traderItem);
                    UserId assignedTraderId = salesItem?.AssignedTraderId
                        ?? traderItem?.AssignedTraderId
                        ?? throw new DomainInvariantException(
                            "Snapshot route is missing Assigned Trader.");
                    DeskId? deskId = deskByTrader.TryGetValue(
                        assignedTraderId.Value, out string? desk)
                        ? DeskId.Create(desk)
                        : null;
                    return new RfqSnapshotRoute(
                        caseId,
                        salesItem?.SalesId,
                        salesItem?.ContactOwnerId ?? traderItem!.ContactOwnerId,
                        assignedTraderId,
                        deskId,
                        salesItem?.CurrentRevisionId ?? traderItem!.CurrentRevisionId,
                        salesItem?.CurrentQuoteId ?? traderItem?.CurrentQuoteId);
                });

        return new BusinessDateRfqSnapshot(
            businessDate,
            generation,
            sales.ToImmutableArray(),
            trader.ToImmutableArray(),
            routes);
    }
}
