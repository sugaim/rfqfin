using Rfq.Domain;

namespace Rfq.Application;

public interface IThemeSettings
{
    Task<AppThemeMode> GetAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<AppThemeMode> SaveAsync(
        UserId userId,
        AppThemeMode mode,
        CancellationToken cancellationToken = default);
}
