using Rfq.Domain;

namespace Rfq.Application;

public sealed class CloseRfqOperation(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IBusinessDateProvider businessDateProvider,
    TimeProvider timeProvider)
{
    public async Task<CloseRfqResult> ApplyAsync(
        CaseId caseId,
        RfqStatus outcome,
        StateVersion expectedCurrentVersion,
        CancellationToken cancellationToken)
    {
        RfqCase rfq = await ClosedRfqUseCase.LoadAsync(
            cases,
            caseId,
            cancellationToken);
        authorization.EnsureCanClose(currentUser.User, rfq);
        DateOnly businessDate = await businessDateProvider.GetCurrentAsync(
            cancellationToken);
        return outcome switch
        {
            RfqStatus.Hit => ClosedRfqUseCase.Apply(
                rfq,
                value => RfqLifecycleTransitions.CloseHit(
                    value,
                    businessDate,
                    expectedCurrentVersion),
                RfqTransitionKind.ClosedHit,
                businessDate,
                cases,
                events,
                currentUser.User,
                timeProvider),
            RfqStatus.Away => ClosedRfqUseCase.Apply(
                rfq,
                value => RfqLifecycleTransitions.CloseAway(
                    value,
                    businessDate,
                    expectedCurrentVersion),
                RfqTransitionKind.ClosedAway,
                businessDate,
                cases,
                events,
                currentUser.User,
                timeProvider),
            _ => throw new RfqRequestValidationException(
                "Close outcome must be Hit or Away."),
        };
    }
}
