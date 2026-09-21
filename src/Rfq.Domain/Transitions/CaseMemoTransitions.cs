namespace Rfq.Domain;

public static class CaseMemoTransitions
{
    public static CaseMemo UpdateSales(
        CaseMemo memo, string? value, StateVersion expectedVersion)
    {
        DomainGuards.EnsureVersion(memo.Version, expectedVersion, "Case Memo");
        return new CaseMemo(memo.CaseId, CaseMemo.Normalize(value),
            memo.TraderMemo, memo.Version.Next());
    }

    public static CaseMemo UpdateTrader(
        CaseMemo memo, string? value, StateVersion expectedVersion)
    {
        DomainGuards.EnsureVersion(memo.Version, expectedVersion, "Case Memo");
        return new CaseMemo(memo.CaseId, memo.SalesMemo,
            CaseMemo.Normalize(value), memo.Version.Next());
    }
}
