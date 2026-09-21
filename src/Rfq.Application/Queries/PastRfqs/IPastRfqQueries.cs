namespace Rfq.Application;

public interface IPastRfqQueries
{
    Task<PastRfqResult> SearchAsync(
        PastRfqSearch search,
        CancellationToken cancellationToken = default);
}
