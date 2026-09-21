using Rfq.Domain;

namespace Rfq.Application;

public sealed class TakeOverRfq(
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
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanTakeOver(currentUser.User, rfqCase, confirmed);
        var previous = rfqCase.AssignedTraderId.Value;
        rfqCase = RfqOwnershipTransitions.TakeOver(
            rfqCase, currentUser.User.UserId, expectedVersion);
        PickUpRfq.Record(events, timeProvider, RfqTransitionKind.TakenOver,
            rfqCase, currentUser.User.UserId, previous);
        return await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken);
    }
}
