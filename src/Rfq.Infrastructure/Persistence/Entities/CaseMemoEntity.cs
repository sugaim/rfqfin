using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class CaseMemoEntity
{
    public long CaseId { get; set; }
    public string SalesMemo { get; set; } = string.Empty;
    public string TraderMemo { get; set; } = string.Empty;
    public long Version { get; set; }
    public RfqCaseEntity RfqCase { get; set; } = null!;
}

