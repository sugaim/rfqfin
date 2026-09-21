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
        CaseId caseId,
        RfqStatus outcome,
        string? reason,
        StateVersion expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanCorrectOutcome(currentUser.User, rfqCase);
        var previous = rfqCase.Status;
        rfqCase = RfqLifecycleTransitions.CorrectOutcome(
            rfqCase, outcome, expectedCurrentVersion);
        rfqCases.Update(rfqCase);
        eventSink.Record(new RfqTransition(
            RfqTransitionKind.OutcomeCorrected,
            caseId,
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            (rfqCase.Lifecycle as ClosedRfq)?.ClosedQuoteId
                ?? throw new DomainInvariantException("Corrected RFQ is not Closed."),
            previous.ToString(),
            outcome.ToString(),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CloseRfq.ToResult(rfqCase);
    }
}
