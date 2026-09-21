namespace Rfq.Domain;

public sealed class CaseMemo
{
    private CaseMemo(
        CaseId caseId,
        string salesMemo,
        string traderMemo,
        long version)
    {
        CaseId = caseId;
        SalesMemo = salesMemo;
        TraderMemo = traderMemo;
        Version = version;
    }

    public CaseId CaseId { get; }

    public string SalesMemo { get; private set; }

    public string TraderMemo { get; private set; }

    public long Version { get; private set; }

    public static CaseMemo Create(CaseId caseId) => new(caseId, string.Empty, string.Empty, 1);

    public static CaseMemo Restore(
        CaseId caseId,
        string salesMemo,
        string traderMemo,
        long version) => new(
            caseId,
            Normalize(salesMemo),
            Normalize(traderMemo),
            version);

    public void UpdateSales(string? memo, long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        SalesMemo = Normalize(memo);
        Version++;
    }

    public void UpdateTrader(string? memo, long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        TraderMemo = Normalize(memo);
        Version++;
    }

    private void EnsureVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException("The Case Memo was changed by another user.");
        }
    }

    private static string Normalize(string? memo) => memo?.Trim() ?? string.Empty;
}
