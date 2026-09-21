using Xunit;

namespace Rfq.Domain.Tests;

public sealed class CaseMemoTests
{
    [Fact]
    public void SalesAndTraderMemosAreIndependentAndVersioned()
    {
        var memo = CaseMemo.Create(new CaseId(1));

        memo.UpdateSales(" sales note ", 1);
        memo.UpdateTrader("trader note", 2);

        Assert.Equal("sales note", memo.SalesMemo);
        Assert.Equal("trader note", memo.TraderMemo);
        Assert.Equal(3, memo.Version);
        Assert.Throws<InvalidOperationException>(() => memo.UpdateSales("stale", 1));
    }
}
