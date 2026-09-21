using Rfq.Domain;

namespace Rfq.Application;

public sealed class CancelRfq(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<LifecycleResult> ExecuteAsync(CaseId caseId, StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfq = await ClosedRfqUseCase.LoadAsync(cases, caseId, cancellationToken);
        authorization.EnsureCanCancelOrReopen(currentUser.User, rfq);
        rfq = RfqLifecycleTransitions.Cancel(rfq, expectedVersion);
        cases.Update(rfq);
        events.Record(new(RfqTransitionKind.Cancelled, caseId,
            currentUser.User.UserId, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WithdrawQuote.ToResult(rfq);
    }
}
