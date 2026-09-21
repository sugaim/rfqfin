using Rfq.Domain;

namespace Rfq.Application;

public sealed class ConfirmQuote(
    IRfqCaseRepository rfqCases,
    IWorkingQuoteRepository workingQuotes,
    IConfirmedQuoteRepository confirmedQuotes,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IQuoteEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<ConfirmQuoteResult> ExecuteAsync(
        long caseId,
        int? expiryMinutes,
        long expectedCurrentVersion,
        long expectedWorkingQuoteVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await UpdateInitialDraft.GetCaseAsync(
            rfqCases,
            caseId,
            cancellationToken);
        authorization.EnsureCanConfirmQuote(currentUser.User, rfqCase);
        if (rfqCase.Version != new StateVersion(expectedCurrentVersion))
        {
            throw new StateVersionMismatchException("The RFQ Case was changed by another user.");
        }

        var workingQuote = await workingQuotes.GetAsync(
            rfqCase.CurrentRevision.RevisionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("WorkingQuote was not found.");
        if (workingQuote.Version != new StateVersion(expectedWorkingQuoteVersion))
        {
            throw new StateVersionMismatchException("The WorkingQuote was changed by another user.");
        }

        var now = timeProvider.GetUtcNow();
        var transition = QuoteTransitions.Confirm(
            rfqCase, workingQuote, QuoteId.New(),
            new QuoteConfirmation(currentUser.User.UserId, now,
                QuoteExpiry.FromMinutes(expiryMinutes)));
        rfqCase = transition.Rfq;
        var confirmedQuote = transition.ConfirmedQuote;
        confirmedQuotes.Add(confirmedQuote);
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Confirmed,
            confirmedQuote.QuoteId.Value,
            currentUser.User.UserId.Value,
            now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfirmQuoteResult(
            caseId,
            confirmedQuote.QuoteId.Value,
            confirmedQuote.RevisionId.Value,
            rfqCase.Status.ToString(),
            rfqCase.QuoteStatus!.Value.ToString(),
            confirmedQuote.Mode.ToString(),
            confirmedQuote.Calculated,
            confirmedQuote.Manual,
            confirmedQuote.ConfirmedAt,
            confirmedQuote.ExpiryMinutes,
            confirmedQuote.ExpiresAt,
            rfqCase.Version.Value);
    }
}
