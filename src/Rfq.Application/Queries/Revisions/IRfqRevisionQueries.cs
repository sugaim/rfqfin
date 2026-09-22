using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqRevisionQueries
{
    Task<IReadOnlyList<RevisionHistoryItem>> GetAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default);
}
