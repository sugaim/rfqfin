namespace Rfq.Infrastructure;

public sealed class RfqRuntimeOptions
{
    public TimeSpan BusinessDatePollInterval { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan SnapshotRefreshCoalesce { get; init; } = TimeSpan.FromMilliseconds(500);
    public int SnapshotRefreshRetryCount { get; init; } = 3;
    public TimeSpan SnapshotRefreshRetryDelay { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan SnapshotRecoveryInterval { get; init; } = TimeSpan.FromSeconds(5);
    public int SearchResultCap { get; init; } = 20_000;
    public int RecentRevisionsDefaultLimit { get; init; } = 50;
    public int RecentRevisionsMaxLimit { get; init; } = 100;
}
