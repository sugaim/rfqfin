namespace Rfq.Application;

public interface IGridConfigStore
{
    Task<GridConfig?> GetAsync(
        string screenId,
        string configKey,
        CancellationToken cancellationToken = default);

    Task<GridConfig> SaveAsync(
        string screenId,
        string configKey,
        int version,
        string config,
        CancellationToken cancellationToken = default);
}
