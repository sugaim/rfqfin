using Rfq.Domain;

namespace Rfq.Application;

public sealed class AssignTrader(
    IRfqCaseRepository rfqCases,
    AssignedTraderValidator assignedTraderValidator,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IRfqEventSink events,
    TimeProvider timeProvider)
{
    public async Task<OwnershipResult> ExecuteAsync(
        CaseId caseId,
        UserId targetTraderId,
        StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanAssignTrader(currentUser.User, rfqCase);
        var target = await assignedTraderValidator.ResolveAsync(
            targetTraderId,
            rfqCase.AssignedTraderId,
            cancellationToken);
        var previous = rfqCase.AssignedTraderId.Value;
        rfqCase = RfqOwnershipTransitions.Assign(
            rfqCase, target, expectedVersion);
        PickUpRfq.Record(events, timeProvider, RfqTransitionKind.AssignedTraderChanged,
            rfqCase, currentUser.User.UserId, previous);
        return await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken);
    }
}
