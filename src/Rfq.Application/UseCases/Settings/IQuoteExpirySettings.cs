using Rfq.Domain;

namespace Rfq.Application;

public interface IQuoteExpirySettings
{
    Task<QuoteExpiry> GetAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<QuoteExpiry> SaveAsync(
        UserId userId,
        QuoteExpiry expiry,
        CancellationToken cancellationToken = default);
}
