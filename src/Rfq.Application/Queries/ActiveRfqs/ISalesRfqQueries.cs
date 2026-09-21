using Rfq.Domain;

namespace Rfq.Application;

public interface ISalesRfqQueries
{
    Task<IReadOnlyList<SalesRfqListItem>> GetAsync(UserId salesUserId,
        CancellationToken cancellationToken = default);
}
