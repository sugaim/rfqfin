using Rfq.Domain;

namespace Rfq.Application;

public sealed class CloseRfq(
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
        StateVersion expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanClose(currentUser.User, rfqCase);
        var transition = RfqLifecycleTransitions.Close(
            rfqCase, outcome, expectedCurrentVersion);
        rfqCase = transition.Rfq;
        rfqCases.Update(rfqCase);
        if (transition.DiscardedRevision is not null)
            rfqCases.UpdateRevision(transition.DiscardedRevision);
        RecordClosed(eventSink, currentUser.User, timeProvider, rfqCase, outcome);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(rfqCase);
    }

    internal static async Task<RfqCase> LoadAsync(
        IRfqCaseRepository rfqCases,
        CaseId caseId,
        CancellationToken cancellationToken) =>
        await rfqCases.GetAsync(caseId, cancellationToken)
            ?? throw new KeyNotFoundException($"RFQ Case '{caseId}' was not found.");

    internal static CloseRfqResult ToResult(RfqCase rfqCase)
    {
        var closed = rfqCase.Lifecycle as ClosedRfq
            ?? throw new DomainInvariantException("Close result requires a Closed RFQ.");
        return new CloseRfqResult(
            rfqCase.CaseId,
            closed.Outcome,
            closed.ClosedQuoteId,
            false,
            rfqCase.Version);
    }

    internal static void RecordClosed(
        IRfqEventSink eventSink,
        CurrentUser user,
        TimeProvider timeProvider,
        RfqCase rfqCase,
        RfqStatus outcome)
    {
        var closed = rfqCase.Lifecycle as ClosedRfq
            ?? throw new DomainInvariantException("Close event requires a Closed RFQ.");
        eventSink.Record(new RfqTransition(
            outcome == RfqStatus.Hit
                ? RfqTransitionKind.ClosedHit
                : RfqTransitionKind.ClosedAway,
            rfqCase.CaseId,
            user.UserId,
            timeProvider.GetUtcNow(),
            closed.ClosedQuoteId));
    }
}
