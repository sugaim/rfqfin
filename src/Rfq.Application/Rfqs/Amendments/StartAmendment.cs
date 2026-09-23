using Rfq.Domain;

namespace Rfq.Application;

public sealed class StartAmendment(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<AmendmentResult> ExecuteAsync(
        StartAmendmentCommand command,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfq = await ClosedRfqUseCase.LoadAsync(cases, command.CaseId, cancellationToken);
        authorization.EnsureCanEditRevision(currentUser.User, rfq);
        AmendmentSaveResult transition = AmendmentTransitions.StartDraft(
            rfq,
            RevisionId.New(),
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            command.ExpectedCurrentVersion);
        rfq = transition.Rfq;
        cases.Update(rfq);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SaveAmendment.ToResult(rfq);
    }
}
