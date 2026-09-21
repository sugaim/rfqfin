using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqHistoryQueries
{
    Task<IReadOnlyList<RevisionHistoryItem>> GetRevisionHistoryAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuoteHistoryItem>> GetQuoteHistoryAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default);
}
