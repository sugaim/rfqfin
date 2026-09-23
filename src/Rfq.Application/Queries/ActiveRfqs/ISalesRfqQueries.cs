using Rfq.Domain;

namespace Rfq.Application;

public interface ISalesRfqQueries
{
    Task<IReadOnlyList<SalesRfqListItem>> GetAsync(
        UserId salesUserId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default);
}
