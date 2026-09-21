using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqCaseRepository
{
    void Add(RfqCase rfqCase);

    Task<RfqCase?> GetAsync(CaseId caseId, CancellationToken cancellationToken = default);

    void Update(RfqCase rfqCase);

    void UpdateRevision(RfqRevision revision);
}
