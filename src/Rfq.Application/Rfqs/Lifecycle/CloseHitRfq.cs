using Rfq.Domain;

namespace Rfq.Application;

public sealed class CloseHitRfq(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CloseRfqResult> ExecuteAsync(
        CaseId caseId,
        StateVersion expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfq = await ClosedRfqUseCase.LoadAsync(cases, caseId, cancellationToken);
        authorization.EnsureCanClose(currentUser.User, rfq);
        return await ClosedRfqUseCase.ApplyAsync(
            rfq,
            value => RfqLifecycleTransitions.CloseHit(value, expectedCurrentVersion),
            RfqTransitionKind.ClosedHit,
            cases,
            events,
            unitOfWork,
            currentUser.User,
            timeProvider,
            cancellationToken);
    }
}
