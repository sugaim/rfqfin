using Rfq.Domain;

namespace Rfq.Application;

public interface IActiveRfqQueries
{
    Task<IReadOnlyList<SalesRfqListItem>> GetSalesAsync(
        UserId salesUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraderRfqListItem>> GetTraderAsync(
        DeskId deskId,
        CancellationToken cancellationToken = default);
}
