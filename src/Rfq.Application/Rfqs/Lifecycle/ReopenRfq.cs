using Rfq.Domain;

namespace Rfq.Application;

public sealed class ReopenRfq(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<LifecycleResult> ExecuteAsync(long caseId, long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, caseId, cancellationToken);
        authorization.EnsureCanCancelOrReopen(currentUser.User, rfq);
        rfq = RfqLifecycleTransitions.Reopen(rfq, new StateVersion(expectedVersion));
        cases.Update(rfq);
        events.Record(new(RfqTransitionKind.Reopened, caseId,
            currentUser.User.UserId.Value, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WithdrawQuote.ToResult(rfq);
    }
}
