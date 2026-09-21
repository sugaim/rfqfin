using Rfq.Domain;

namespace Rfq.Application;

public sealed record GridConfig(string ScreenId, string ConfigKey, int Version,
    string ConfigJson, DateTimeOffset UpdatedAt);
