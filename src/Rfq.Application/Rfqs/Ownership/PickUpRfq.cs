using Rfq.Domain;

namespace Rfq.Application;

public sealed class PickUpRfq(
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
        RfqCase rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanPickUp(currentUser.User, rfqCase, confirmed);
        rfqCase = RfqOwnershipTransitions.PickUp(
            rfqCase, currentUser.User.UserId, expectedVersion);
        Record(events, timeProvider, RfqTransitionKind.PickedUp, rfqCase, currentUser.User.UserId);
        return await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken);
    }

    internal static void Record(
        IRfqEventSink events,
        TimeProvider timeProvider,
        RfqTransitionKind kind,
        RfqCase rfqCase,
        UserId actor,
        string? from = null) =>
        events.Record(new RfqTransition(
            kind,
            rfqCase.CaseId,
            actor,
            timeProvider.GetUtcNow(),
            From: from,
            To: rfqCase.AssignedTraderId.Value));
}
