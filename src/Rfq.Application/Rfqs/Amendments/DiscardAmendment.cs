using Rfq.Domain;

namespace Rfq.Application;

public sealed class DiscardAmendment(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<AmendmentResult> ExecuteAsync(
        AmendmentItem command,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, command.CaseId, cancellationToken);
        authorization.EnsureCanDiscardRevision(currentUser.User, rfq);
        var transition = AmendmentTransitions.Discard(
            rfq, new StateVersion(command.ExpectedCurrentVersion),
            new StateVersion(command.ExpectedDraftVersion));
        rfq = transition.Rfq;
        cases.Update(rfq);
        cases.UpdateRevision(transition.DiscardedRevision);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SaveAmendment.ToResult(rfq);
    }
}
