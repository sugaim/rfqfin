using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class UserGridConfigEntity
{
    public string UserId { get; set; } = string.Empty;
    public string ScreenId { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}

