using Rfq.Domain;

namespace Rfq.Application;

public interface ICaseMemoRepository
{
    Task<CaseMemo?> GetAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default);

    void Update(CaseMemo memo);
}
