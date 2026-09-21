using Rfq.Domain;

namespace Rfq.Application;

public interface IConfirmedQuoteRepository
{
    void Add(ConfirmedQuote quote);

    Task<ConfirmedQuote?> GetAsync(
        QuoteId quoteId,
        CancellationToken cancellationToken = default);
}
