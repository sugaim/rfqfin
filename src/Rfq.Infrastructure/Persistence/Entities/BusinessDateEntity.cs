namespace Rfq.Infrastructure;

internal sealed class BusinessDateEntity
{
    public string Key { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
}
