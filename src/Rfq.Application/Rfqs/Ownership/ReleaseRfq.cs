using Rfq.Domain;

namespace Rfq.Application;

public sealed class ReleaseRfq(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IRfqEventSink? events = null,
    TimeProvider? timeProvider = null)
{
    public async Task<OwnershipResult> ExecuteAsync(
        long caseId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanRelease(currentUser.User, rfqCase);
        rfqCase = RfqOwnershipTransitions.Release(
            rfqCase, currentUser.User.UserId, new StateVersion(expectedVersion));
        PickUpRfq.Record(events, timeProvider, RfqTransitionKind.Released,
            rfqCase, currentUser.User.UserId);
        return await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken);
    }
}
