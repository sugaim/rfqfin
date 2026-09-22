using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class RfqMemoRepository(RfqDbContext dbContext) : IRfqMemoRepository
{
    public async Task<SalesMemo?> GetSalesAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default)
    {
        SalesMemoEntity? entity = await dbContext.SalesMemos.SingleOrDefaultAsync(
            item => item.CaseId == caseId.Value, cancellationToken);
        return entity is null ? null : SalesMemo.Restore(
            new CaseId(entity.CaseId),
            entity.Value,
            new StateVersion(entity.Version));
    }

    public async Task<TraderMemo?> GetTraderAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default)
    {
        TraderMemoEntity? entity = await dbContext.TraderMemos.SingleOrDefaultAsync(
            item => item.CaseId == caseId.Value, cancellationToken);
        return entity is null ? null : TraderMemo.Restore(
            new CaseId(entity.CaseId),
            entity.Value,
            new StateVersion(entity.Version));
    }

    public void Update(SalesMemo memo)
    {
        SalesMemoEntity entity = dbContext.SalesMemos.Local.SingleOrDefault(
            item => item.CaseId == memo.CaseId.Value)
            ?? throw new InvalidOperationException("The Sales Memo must be loaded before update.");
        entity.Value = memo.Value;
        entity.Version = memo.Version.Value;
    }

    public void Update(TraderMemo memo)
    {
        TraderMemoEntity entity = dbContext.TraderMemos.Local.SingleOrDefault(
            item => item.CaseId == memo.CaseId.Value)
            ?? throw new InvalidOperationException("The Trader Memo must be loaded before update.");
        entity.Value = memo.Value;
        entity.Version = memo.Version.Value;
    }
}
