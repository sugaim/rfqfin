namespace Rfq.Domain;

public static class TraderMemoTransitions
{
    public static TraderMemo Update(TraderMemo memo, string? value, StateVersion expectedVersion)
    {
        DomainGuards.EnsureVersion(memo.Version, expectedVersion, "Trader Memo");
        return new TraderMemo(memo.CaseId, TraderMemo.Normalize(value), memo.Version.Next());
    }
}
