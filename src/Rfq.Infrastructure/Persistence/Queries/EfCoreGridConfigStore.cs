using Microsoft.EntityFrameworkCore;
using Rfq.Application;

namespace Rfq.Infrastructure;

public sealed class EfCoreGridConfigStore(
    RfqDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IGridConfigStore
{
    public async Task<GridConfig?> GetAsync(
        string screenId,
        string configKey,
        CancellationToken cancellationToken = default)
    {
        UserGridConfigEntity? item = await dbContext.UserGridConfigs.AsNoTracking().SingleOrDefaultAsync(
            value => value.UserId == currentUser.User.UserId.Value
                && value.ScreenId == screenId && value.ConfigKey == configKey,
            cancellationToken);
        return item is null ? null : ToConfig(item);
    }

    public async Task<GridConfig> SaveAsync(
        string screenId,
        string configKey,
        int version,
        string config,
        CancellationToken cancellationToken = default)
    {
        string userId = currentUser.User.UserId.Value;
        UserGridConfigEntity? item = await dbContext.UserGridConfigs.SingleOrDefaultAsync(
            value =>
                value.UserId == userId && value.ScreenId == screenId && value.ConfigKey == configKey,
            cancellationToken);
        if (item is null)
        {
            item = new() { UserId = userId, ScreenId = screenId, ConfigKey = configKey };
            dbContext.UserGridConfigs.Add(item);
        }
        item.Version = version;
        item.ConfigJson = config;
        item.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToConfig(item);
    }

    private static GridConfig ToConfig(UserGridConfigEntity item) => new(
        item.ScreenId, item.ConfigKey, item.Version, item.ConfigJson, item.UpdatedAt);
}
