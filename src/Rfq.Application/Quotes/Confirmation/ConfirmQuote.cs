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
        CaseId caseId,
        QuoteExpiry expiry,
        StateVersion expectedCurrentVersion,
        StateVersion expectedWorkingQuoteVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await UpdateInitialDraft.GetCaseAsync(
            rfqCases,
            caseId,
            cancellationToken);
        authorization.EnsureCanConfirmQuote(currentUser.User, rfqCase);
        if (rfqCase.Version != expectedCurrentVersion)
        {
            throw new StateVersionMismatchException("The RFQ Case was changed by another user.");
        }

        var workingQuote = await workingQuotes.GetAsync(
            rfqCase.CurrentRevision.RevisionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("WorkingQuote was not found.");
        if (workingQuote.Version != expectedWorkingQuoteVersion)
        {
            throw new StateVersionMismatchException("The WorkingQuote was changed by another user.");
        }

        var now = timeProvider.GetUtcNow();
        var transition = QuoteTransitions.Confirm(
            rfqCase, workingQuote, QuoteId.New(),
            new QuoteConfirmation(currentUser.User.UserId, now, expiry));
        rfqCase = transition.Rfq;
        var confirmedQuote = transition.ConfirmedQuote;
        confirmedQuotes.Add(confirmedQuote);
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Confirmed,
            confirmedQuote.QuoteId,
            currentUser.User.UserId,
            now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfirmQuoteResult(
            caseId,
            confirmedQuote.QuoteId,
            confirmedQuote.RevisionId,
            rfqCase.Status,
            rfqCase.QuoteStatus
                ?? throw new DomainInvariantException("Confirmed RFQ is missing QuoteStatus."),
            confirmedQuote.Mode,
            confirmedQuote.Calculated,
            confirmedQuote.Manual,
            confirmedQuote.ConfirmedAt,
            expiry,
            confirmedQuote.ExpiresAt,
            rfqCase.Version);
    }
}

public sealed record ConfirmQuoteResult(
    CaseId CaseId,
    QuoteId QuoteId,
    RevisionId RevisionId,
    RfqStatus RfqStatus,
    QuoteStatus QuoteStatus,
    WorkingQuoteMode Mode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    DateTimeOffset ConfirmedAt,
    QuoteExpiry Expiry,
    DateTimeOffset? ExpiresAt,
    StateVersion CurrentVersion);
