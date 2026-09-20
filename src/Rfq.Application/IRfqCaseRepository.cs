using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqCaseRepository
{
    void Add(RfqCase rfqCase);

    Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(
        UserId salesUserId,
        CancellationToken cancellationToken = default);
}
