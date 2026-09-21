using Rfq.Domain;

namespace Rfq.Application;

public sealed record QuoteEditContext(
    CaseId CaseId,
    RevisionId RevisionId,
    SecurityId SecurityId,
    DateOnly SettlementDate,
    StateVersion Version,
    UserId AssignedTraderId,
    Ownership Ownership,
    ActiveQuoteState QuoteState,
    WorkingQuote WorkingQuote);

internal static class WorkingQuoteMutation
{
    public static async Task<(RfqCase Case, WorkingQuote Quote)> LoadAsync(
        IRfqCaseRepository rfqCases,
        IWorkingQuoteRepository workingQuotes,
        IRfqAuthorization authorization,
        CurrentUser user,
        CaseId caseId,
        StateVersion expectedCurrentVersion,
        StateVersion expectedWorkingQuoteVersion,
        CancellationToken cancellationToken)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        if (rfqCase.Version != expectedCurrentVersion)
        {
            throw new InvalidOperationException("The RFQ was changed by another user.");
        }

        authorization.EnsureCanQuote(user, CalculateWorkingQuote.ToAuthorizationState(rfqCase));
        var quote = await workingQuotes.GetAsync(
            rfqCase.CurrentRevision.RevisionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("WorkingQuote was not found.");
        if (quote.Version != expectedWorkingQuoteVersion)
        {
            throw new InvalidOperationException("The WorkingQuote was changed by another user.");
        }

        return (rfqCase, quote);
    }
}

public sealed record WorkingQuoteResult(
    CaseId CaseId,
    RevisionId RevisionId,
    WorkingQuoteMode Mode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    StateVersion Version,
    StateVersion CurrentVersion)
{
    public static WorkingQuoteResult From(
        CaseId caseId,
        WorkingQuote quote,
        StateVersion currentVersion) => new(
        caseId,
        quote.RevisionId,
        quote.Mode,
        quote.Calculated,
        quote.Manual,
        quote.Version,
        currentVersion);
}
