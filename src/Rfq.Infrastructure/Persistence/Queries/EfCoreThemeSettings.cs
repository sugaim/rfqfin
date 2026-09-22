using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreThemeSettings(RfqDbContext dbContext) : IThemeSettings
{
    public async Task<AppThemeMode> GetAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var setting = await dbContext.MasterUsers.AsNoTracking()
            .Where(item => item.UserId == userId.Value)
            .Select(item => new { item.Theme })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RfqInvariantException($"User '{userId.Value}' was not found.");
        return setting.Theme ?? AppThemeMode.Dark;
    }

    public async Task<AppThemeMode> SaveAsync(
        UserId userId,
        AppThemeMode mode,
        CancellationToken cancellationToken = default)
    {
        MasterUserEntity user = await dbContext.MasterUsers.SingleOrDefaultAsync(
            item => item.UserId == userId.Value, cancellationToken)
            ?? throw new RfqInvariantException($"User '{userId.Value}' was not found.");
        user.Theme = mode;
        await dbContext.SaveChangesAsync(cancellationToken);
        return mode;
    }
}
