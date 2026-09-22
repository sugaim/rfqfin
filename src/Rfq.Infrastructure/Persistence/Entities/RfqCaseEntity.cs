using Rfq.Domain;

namespace Rfq.Infrastructure;

internal sealed class RfqCaseEntity
{
    public long CaseId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string SecurityId { get; set; } = string.Empty;
    public string CategorySnapshot { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateOnly? CreatedBusinessDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? SalesId { get; set; }
    public long? CopiedFromCaseId { get; set; }
    public List<RfqRevisionEntity> Revisions { get; set; } = [];
    public CaseCurrentEntity Current { get; set; } = null!;
    public SalesMemoEntity SalesMemo { get; set; } = null!;
    public TraderMemoEntity TraderMemo { get; set; } = null!;
}
