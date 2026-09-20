namespace Rfq.Infrastructure;

public sealed class SeedMarker
{
    private SeedMarker()
    {
    }

    public SeedMarker(string key, DateTimeOffset appliedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Key = key;
        AppliedAt = appliedAt;
    }

    public string Key { get; private set; } = string.Empty;

    public DateTimeOffset AppliedAt { get; private set; }
}
