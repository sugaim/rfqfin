namespace Rfq.Application;

public interface IEodQueries
{
    Task<IReadOnlyList<EodSummaryItem>> GetEodAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);
}
