using Rfq.Domain;

namespace Rfq.Application;

public sealed record WorkingQuoteResult(
    long CaseId,
    Guid RevisionId,
    string Mode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    long Version,
    long CurrentVersion)
{
    public static WorkingQuoteResult From(
        long caseId,
        WorkingQuote quote,
        long currentVersion) => new(
        caseId,
        quote.RevisionId.Value,
        quote.Mode.ToString(),
        quote.Calculated,
        quote.Manual,
        quote.Version.Value,
        currentVersion);
}
