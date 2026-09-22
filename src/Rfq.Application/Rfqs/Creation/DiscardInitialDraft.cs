using Rfq.Domain;

namespace Rfq.Application;

public sealed class DiscardInitialDraft(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(
        CaseId caseId,
        StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfqCase = await UpdateInitialDraft.GetCaseAsync(
            rfqCases,
            caseId,
            cancellationToken);
        authorization.EnsureCanDiscardRevision(currentUser.User, rfqCase);
        rfqCase = InitialDraftTransitions.Discard(
            rfqCase, expectedVersion);
        rfqCases.Update(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
