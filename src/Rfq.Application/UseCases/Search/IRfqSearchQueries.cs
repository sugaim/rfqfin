namespace Rfq.Application;

public interface IRfqSearchQueries
{
    Task<RfqSearchResult> SearchAsync(
        RfqSearch search,
        CancellationToken cancellationToken = default);
}
