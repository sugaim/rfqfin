using Rfq.Domain;

namespace Rfq.Application;

public sealed class DiscardInitialDraft(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(
        long caseId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await UpdateInitialDraft.GetCaseAsync(
            rfqCases,
            caseId,
            cancellationToken);
        authorization.EnsureCanDiscardRevision(currentUser.User, rfqCase);
        rfqCase = InitialDraftTransitions.Discard(
            rfqCase, new StateVersion(expectedVersion));
        rfqCases.Update(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
