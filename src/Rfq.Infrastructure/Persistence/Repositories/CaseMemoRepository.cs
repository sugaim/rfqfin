using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class CaseMemoRepository(RfqDbContext dbContext) : ICaseMemoRepository
{
    public async Task<CaseMemo?> GetAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CaseMemos
            .SingleOrDefaultAsync(item => item.CaseId == caseId.Value, cancellationToken);
        return entity is null
            ? null
            : CaseMemo.Restore(
                new CaseId(entity.CaseId),
                entity.SalesMemo,
                entity.TraderMemo,
                new StateVersion(entity.Version));
    }

    public void Update(CaseMemo memo)
    {
        var entity = dbContext.CaseMemos.Local
            .SingleOrDefault(item => item.CaseId == memo.CaseId.Value)
            ?? throw new InvalidOperationException(
                "The Case Memo must be loaded before it can be updated.");
        entity.SalesMemo = memo.SalesMemo;
        entity.TraderMemo = memo.TraderMemo;
        entity.Version = memo.Version.Value;
    }
}
