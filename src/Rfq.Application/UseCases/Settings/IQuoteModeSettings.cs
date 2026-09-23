using Rfq.Domain;

namespace Rfq.Application;

public interface IQuoteModeSettings
{
    Task<WorkingQuoteMode> GetAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<WorkingQuoteMode> SaveAsync(
        UserId userId,
        WorkingQuoteMode mode,
        CancellationToken cancellationToken = default);
}
