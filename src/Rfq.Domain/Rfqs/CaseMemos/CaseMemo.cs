namespace Rfq.Domain;

public sealed class CaseMemo
{
    internal CaseMemo(CaseId caseId, string salesMemo, string traderMemo, StateVersion version)
    {
        CaseId = caseId;
        SalesMemo = Normalize(salesMemo);
        TraderMemo = Normalize(traderMemo);
        Version = version;
    }
    public CaseId CaseId { get; }
    public string SalesMemo { get; }
    public string TraderMemo { get; }
    public StateVersion Version { get; }
    public static CaseMemo Create(CaseId caseId) => new(caseId, "", "", new StateVersion(1));
    internal static CaseMemo Restore(CaseId id, string sales, string trader, StateVersion version) =>
        new(id, sales, trader, version);
    internal static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}
