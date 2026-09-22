using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqMemoRepository
{
    Task<SalesMemo?> GetSalesAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default);

    Task<TraderMemo?> GetTraderAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default);

    void Update(SalesMemo memo);
    void Update(TraderMemo memo);
}
