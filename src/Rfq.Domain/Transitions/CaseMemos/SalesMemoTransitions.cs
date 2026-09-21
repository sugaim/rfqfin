namespace Rfq.Domain;

public static class SalesMemoTransitions
{
    public static SalesMemo Update(SalesMemo memo, string? value, StateVersion expectedVersion)
    {
        DomainGuards.EnsureVersion(memo.Version, expectedVersion, "Sales Memo");
        return new SalesMemo(memo.CaseId, SalesMemo.Normalize(value), memo.Version.Next());
    }
}
