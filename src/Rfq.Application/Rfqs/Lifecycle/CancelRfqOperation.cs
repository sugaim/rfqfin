using Rfq.Domain;

namespace Rfq.Application;

public sealed class CancelRfqOperation(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IBusinessDateProvider businessDateProvider,
    TimeProvider timeProvider)
{
    public async Task<LifecycleResult> ApplyAsync(
        CaseId caseId,
        StateVersion expectedVersion,
        CancellationToken cancellationToken)
    {
        RfqCase rfq = await ClosedRfqUseCase.LoadAsync(
            cases,
            caseId,
            cancellationToken);
        authorization.EnsureCanCancelOrReopen(currentUser.User, rfq);
        DateOnly businessDate = await businessDateProvider.GetCurrentAsync(
            cancellationToken);
        rfq = RfqLifecycleTransitions.Cancel(rfq, expectedVersion);
        cases.Update(rfq);
        events.Record(new(
            RfqTransitionKind.Cancelled,
            caseId,
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            BusinessDate: businessDate));
        return WithdrawQuote.ToResult(rfq);
    }
}
