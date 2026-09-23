using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class CachedSalesRfqQueries(BusinessDateRfqSnapshotCoordinator coordinator)
    : ISalesRfqQueries
{
    public async Task<IReadOnlyList<SalesRfqListItem>> GetAsync(
        UserId salesUserId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        BusinessDateRfqSnapshot snapshot = await coordinator.GetSnapshotAsync(
            businessDate, cancellationToken);
        return [.. snapshot.Sales.Where(item =>
            item.SalesId == salesUserId || item.ContactOwnerId == salesUserId)];
    }
}

internal sealed class CachedTraderRfqQueries(BusinessDateRfqSnapshotCoordinator coordinator)
    : ITraderRfqQueries
{
    public async Task<IReadOnlyList<TraderRfqListItem>> GetAsync(
        DeskId deskId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        BusinessDateRfqSnapshot snapshot = await coordinator.GetSnapshotAsync(
            businessDate, cancellationToken);
        return [.. snapshot.Trader.Where(item =>
            snapshot.Routes[item.CaseId].AssignedTraderDeskId == deskId)];
    }
}
