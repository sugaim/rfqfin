namespace Rfq.Application;

public interface IEodQueries
{
    Task<IReadOnlyList<EodSummaryItem>> GetEodAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default);
}
