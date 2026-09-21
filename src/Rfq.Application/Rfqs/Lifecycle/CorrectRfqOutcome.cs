using Rfq.Domain;

namespace Rfq.Application;

public sealed class CorrectRfqOutcome(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CloseRfqResult> ExecuteAsync(
        long caseId,
        RfqStatus outcome,
        string? reason,
        long expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanCorrectOutcome(currentUser.User, rfqCase);
        var previous = rfqCase.Status;
        rfqCase = RfqLifecycleTransitions.CorrectOutcome(
            rfqCase, outcome, new StateVersion(expectedCurrentVersion));
        rfqCases.Update(rfqCase);
        eventSink.Record(new RfqTransition(
            RfqTransitionKind.OutcomeCorrected,
            caseId,
            currentUser.User.UserId.Value,
            timeProvider.GetUtcNow(),
            rfqCase.ClosedQuoteId!.Value.Value,
            previous.ToString(),
            outcome.ToString(),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CloseRfq.ToResult(rfqCase);
    }
}
