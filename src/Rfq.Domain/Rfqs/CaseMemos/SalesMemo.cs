namespace Rfq.Domain;

public sealed class SalesMemo
{
    internal SalesMemo(CaseId caseId, string value, StateVersion version)
    {
        CaseId = caseId;
        Value = Normalize(value);
        Version = version;
    }

    public CaseId CaseId { get; }
    public string Value { get; }
    public StateVersion Version { get; }
    public static SalesMemo Create(CaseId caseId) => new(caseId, "", new StateVersion(1));

    internal static SalesMemo Restore(CaseId id, string value, StateVersion version) =>
        new(id, value, version);

    internal static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}
