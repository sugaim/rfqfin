namespace Rfq.Infrastructure;

internal sealed class TraderMemoEntity
{
    public long CaseId { get; set; }
    public string Value { get; set; } = string.Empty;
    public long Version { get; set; }
    public RfqCaseEntity RfqCase { get; set; } = null!;
}
