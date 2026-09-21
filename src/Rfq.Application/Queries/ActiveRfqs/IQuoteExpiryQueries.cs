namespace Rfq.Application;

public interface IQuoteExpiryQueries
{
    Task<IReadOnlyList<ExpiredQuoteCandidate>> GetExpiredAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
