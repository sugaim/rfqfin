namespace Rfq.Infrastructure;

internal sealed class SystemDateEntity
{
    public string Key { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
}

