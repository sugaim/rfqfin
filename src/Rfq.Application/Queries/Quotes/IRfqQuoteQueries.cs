using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqQuoteQueries
{
    Task<IReadOnlyList<QuoteHistoryItem>> GetAsync(CaseId caseId,
        CancellationToken cancellationToken = default);
}
