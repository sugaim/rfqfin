using Rfq.Domain;

namespace Rfq.Application;

public interface IOperationalQueries
{
    Task<PastRfqResult> SearchAsync(PastRfqSearch search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RevisionHistoryItem>> GetRevisionHistoryAsync(long caseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuoteHistoryItem>> GetQuoteHistoryAsync(long caseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EodSummaryItem>> GetEodAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<GridConfig?> GetGridConfigAsync(string screenId, string configKey, CancellationToken cancellationToken = default);
    Task<GridConfig> SaveGridConfigAsync(string screenId, string configKey, int version, string configJson, CancellationToken cancellationToken = default);
}
