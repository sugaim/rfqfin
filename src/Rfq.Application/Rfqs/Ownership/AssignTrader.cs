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
    public async Task<AssignTraderResult> ExecuteAsync(
        CaseId caseId,
        UserId targetTraderId,
        StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanAssignTrader(currentUser.User, rfqCase);
        UserId target = await assignedTraderValidator.ResolveAsync(
            targetTraderId,
            cancellationToken);
        if (rfqCase.Version != expectedVersion)
        {
            throw new StateVersionMismatchException("The RFQ Case was changed by another user.");
        }

        if (rfqCase.AssignedTraderId == target)
        {
            return new AssignTraderResult(
                AssignTraderOutcome.AlreadyAssigned,
                OwnershipUseCase.ToResult(rfqCase));
        }
        string previous = rfqCase.AssignedTraderId.Value;
        rfqCase = RfqOwnershipTransitions.Assign(
            rfqCase, target, expectedVersion);
        PickUpRfq.Record(
            events,
            timeProvider,
            RfqTransitionKind.AssignedTraderChanged,
            rfqCase,
            currentUser.User.UserId,
            previous);
        return new AssignTraderResult(
            AssignTraderOutcome.Assigned,
            await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken));
    }
}

public enum AssignTraderOutcome
{
    Assigned,
    AlreadyAssigned
}

public sealed record AssignTraderResult(AssignTraderOutcome Outcome, OwnershipResult Rfq);
