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
        long caseId,
        RfqStatus outcome,
        long expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanClose(currentUser.User, rfqCase);
        var transition = RfqLifecycleTransitions.Close(
            rfqCase, outcome, new StateVersion(expectedCurrentVersion));
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
        long caseId,
        CancellationToken cancellationToken) =>
        await rfqCases.GetAsync(new CaseId(caseId), cancellationToken)
            ?? throw new KeyNotFoundException($"RFQ Case '{caseId}' was not found.");

    internal static CloseRfqResult ToResult(RfqCase rfqCase) => new(
        rfqCase.CaseId.Value,
        rfqCase.Status.ToString(),
        rfqCase.ClosedQuoteId!.Value.Value,
        rfqCase.Ownership is Owned,
        rfqCase.Version.Value);

    internal static void RecordClosed(
        IRfqEventSink eventSink,
        CurrentUser user,
        TimeProvider timeProvider,
        RfqCase rfqCase,
        RfqStatus outcome) => eventSink.Record(new RfqTransition(
            outcome == RfqStatus.Hit
                ? RfqTransitionKind.ClosedHit
                : RfqTransitionKind.ClosedAway,
            rfqCase.CaseId.Value,
            user.UserId.Value,
            timeProvider.GetUtcNow(),
            rfqCase.ClosedQuoteId!.Value.Value));
}
