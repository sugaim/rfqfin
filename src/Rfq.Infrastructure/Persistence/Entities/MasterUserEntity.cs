namespace Rfq.Infrastructure;

internal sealed class MasterUserEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeskId { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
    public int? DefaultQuoteExpiryMinutes { get; set; }
}

