using Rfq.Domain;

namespace Rfq.Application;

internal static class WorkingQuoteMutation
{
    public static async Task<(RfqCase Case, WorkingQuote Quote)> LoadAsync(
        IRfqCaseRepository rfqCases,
        IWorkingQuoteRepository workingQuotes,
        IRfqAuthorization authorization,
        CurrentUser user,
        long caseId,
        long expectedCurrentVersion,
        long expectedWorkingQuoteVersion,
        CancellationToken cancellationToken)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        if (rfqCase.Version != new StateVersion(expectedCurrentVersion))
        {
            throw new InvalidOperationException("The RFQ was changed by another user.");
        }

        authorization.EnsureCanQuote(user, CalculateWorkingQuote.ToAuthorizationState(rfqCase));
        var quote = await workingQuotes.GetAsync(
            rfqCase.CurrentRevision.RevisionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("WorkingQuote was not found.");
        if (quote.Version != new StateVersion(expectedWorkingQuoteVersion))
        {
            throw new InvalidOperationException("The WorkingQuote was changed by another user.");
        }

        return (rfqCase, quote);
    }
}
