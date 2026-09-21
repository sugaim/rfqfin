using Rfq.Domain;

namespace Rfq.Application;

public sealed class ChangeContactOwner(
    IRfqCaseRepository rfqCases,
    IUserDirectory users,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<ContactOwnerResult> ExecuteAsync(
        long caseId,
        string targetUserId,
        long expectedCurrentVersion,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            throw new ArgumentException("Contact Owner handoff requires confirmation.", nameof(confirmed));
        }

        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanChangeContactOwner(currentUser.User, rfqCase);
        var targetId = UserId.Create(targetUserId);
        var target = await users.ResolveAsync(targetId, cancellationToken)
            ?? throw new ArgumentException($"User '{targetUserId}' was not found.", nameof(targetUserId));
        if (target.DeskId != currentUser.User.DeskId
            || !target.Roles.Any(role => role is UserRole.Sales or UserRole.Trader))
        {
            throw new ArgumentException(
                "Contact Owner must be a Sales or Trader user on the same desk.",
                nameof(targetUserId));
        }

        var previous = rfqCase.ContactOwnerId.Value;
        rfqCase = RfqResponsibilityTransitions.ChangeContactOwner(
            rfqCase, targetId, new StateVersion(expectedCurrentVersion));
        rfqCases.Update(rfqCase);
        eventSink.Record(new RfqTransition(
            RfqTransitionKind.ContactOwnerChanged,
            caseId,
            currentUser.User.UserId.Value,
            timeProvider.GetUtcNow(),
            From: previous,
            To: targetId.Value));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ContactOwnerResult(caseId, targetId.Value, rfqCase.Version.Value);
    }
}
