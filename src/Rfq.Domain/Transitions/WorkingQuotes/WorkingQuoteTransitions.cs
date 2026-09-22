namespace Rfq.Domain;

public static class WorkingQuoteTransitions
{
    public static WorkingQuote ApplyCalculated(
        WorkingQuote quote,
        CalculatedQuotePayload payload,
        StateVersion expectedVersion,
        UserId updatedBy,
        DateTimeOffset updatedAt)
    {
        EnsureVersion(quote, expectedVersion);
        return Copy(quote, WorkingQuoteMode.Calculated, payload, quote.Manual, updatedBy, updatedAt);
    }

    public static WorkingQuote SwitchMode(
        WorkingQuote quote,
        WorkingQuoteMode mode,
        StateVersion expectedVersion,
        UserId updatedBy,
        DateTimeOffset updatedAt)
    {
        EnsureVersion(quote, expectedVersion);
        ManualQuotePayload? manual = mode == WorkingQuoteMode.Manual && quote.Mode != WorkingQuoteMode.Manual
            ? new ManualQuotePayload(null, null)
            : quote.Manual;
        return Copy(quote, mode, quote.Calculated, manual, updatedBy, updatedAt);
    }

    public static WorkingQuote UpdateManual(
        WorkingQuote quote,
        decimal? price,
        decimal? finalSimpleYield,
        StateVersion expectedVersion,
        UserId updatedBy,
        DateTimeOffset updatedAt)
    {
        EnsureVersion(quote, expectedVersion);
        if (quote.Mode != WorkingQuoteMode.Manual)
        {
            throw new DomainRuleViolationException(
                "Manual values can only be edited in Manual mode.");
        }

        return Copy(
            quote,
            quote.Mode,
            quote.Calculated,
            new ManualQuotePayload(price, finalSimpleYield),
            updatedBy,
            updatedAt);
    }

    private static void EnsureVersion(WorkingQuote quote, StateVersion expected) =>
        DomainGuards.EnsureVersion(quote.Version, expected, "WorkingQuote");

    private static WorkingQuote Copy(
        WorkingQuote quote,
        WorkingQuoteMode mode,
        CalculatedQuotePayload? calculated,
        ManualQuotePayload? manual,
        UserId updatedBy,
        DateTimeOffset updatedAt) => new(
            quote.RevisionId,
            mode,
            calculated,
            manual,
            quote.Version.Next(),
            quote.CreatedAt,
            quote.CreatedBy,
            updatedAt,
            updatedBy);
}
