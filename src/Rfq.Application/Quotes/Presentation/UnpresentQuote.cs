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
        long caseId,
        long expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await UpdateInitialDraft.GetCaseAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanPresent(currentUser.User, rfqCase);
        rfqCase = RfqLifecycleTransitions.Unpresent(
            rfqCase, new StateVersion(expectedCurrentVersion));
        var quoteId = rfqCase.CurrentQuoteId
            ?? throw new InvalidOperationException("Current ConfirmedQuote was not found.");
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Unpresented,
            quoteId.Value,
            currentUser.User.UserId.Value,
            timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PresentationResult(
            caseId,
            quoteId.Value,
            rfqCase.Status.ToString(),
            rfqCase.QuoteStatus!.Value.ToString(),
            rfqCase.Version.Value);
    }
}
