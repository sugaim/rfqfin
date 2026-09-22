using Rfq.Domain;

namespace Rfq.Application;

public sealed class ReleaseRfq(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IRfqEventSink events,
    TimeProvider timeProvider)
{
    public async Task<OwnershipResult> ExecuteAsync(
        CaseId caseId,
        StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanRelease(currentUser.User, rfqCase);
        rfqCase = RfqOwnershipTransitions.Release(
            rfqCase, expectedVersion);
        PickUpRfq.Record(
            events,
            timeProvider,
            RfqTransitionKind.Released,
            rfqCase,
            currentUser.User.UserId);
        return await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken);
    }
}
