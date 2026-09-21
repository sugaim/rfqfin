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
        CaseId caseId,
        UserId targetUserId,
        StateVersion expectedCurrentVersion,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            throw new ArgumentException("Contact Owner handoff requires confirmation.", nameof(confirmed));
        }

        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanChangeContactOwner(currentUser.User, rfqCase);
        var target = await users.ResolveAsync(targetUserId, cancellationToken)
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
            rfqCase, targetUserId, expectedCurrentVersion);
        rfqCases.Update(rfqCase);
        eventSink.Record(new RfqTransition(
            RfqTransitionKind.ContactOwnerChanged,
            caseId,
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            From: previous,
            To: targetUserId.Value));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ContactOwnerResult(caseId, targetUserId, rfqCase.Version);
    }
}

public sealed record ContactOwnerResult(
    CaseId CaseId,
    UserId ContactOwnerId,
    StateVersion CurrentVersion);
