using Rfq.Domain;

namespace Rfq.Application;

public interface ITraderRfqQueries
{
    Task<IReadOnlyList<TraderRfqListItem>> GetAsync(
        DeskId deskId,
        CancellationToken cancellationToken = default);
}
