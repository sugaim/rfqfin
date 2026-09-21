using Rfq.Domain;

namespace Rfq.Application;

public sealed class UnpresentQuote(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IQuoteEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<PresentationResult> ExecuteAsync(
        CaseId caseId,
        StateVersion expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await UpdateInitialDraft.GetCaseAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanPresent(currentUser.User, rfqCase);
        rfqCase = RfqLifecycleTransitions.Unpresent(
            rfqCase, expectedCurrentVersion);
        var quoteId = rfqCase.CurrentQuoteId
            ?? throw new RfqInvariantException("Current ConfirmedQuote was not found.");
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Unpresented,
            quoteId,
            currentUser.User.UserId,
            timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PresentationResult(
            caseId,
            quoteId,
            rfqCase.Status,
            rfqCase.QuoteStatus
                ?? throw new DomainInvariantException("Active quoted RFQ is missing QuoteStatus."),
            rfqCase.Version);
    }
}
