using Rfq.Domain;

namespace Rfq.Application;

public sealed class SaveAmendment(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<AmendmentResult> ExecuteAsync(
        SaveAmendmentCommand command,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfq = await ClosedRfqUseCase.LoadAsync(cases, command.CaseId, cancellationToken);
        authorization.EnsureCanEditRevision(currentUser.User, rfq);
        AmendmentSaveResult transition = AmendmentTransitions.SaveDraft(
            rfq,
            RevisionId.New(),
            new RevisionTerms(
                command.Notional,
                command.SettlementDate,
                rfq.CurrentRevision.StandardSettlementDate,
                command.SalesAndTradingMessage),
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            command.ExpectedCurrentVersion,
            command.ExpectedDraftVersion);
        rfq = transition.Rfq;
        cases.Update(rfq);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(rfq);
    }

    internal static AmendmentResult ToResult(RfqCase rfq) => new(
        rfq.CaseId,
        rfq.CurrentRevision.RevisionId,
        rfq.PendingDraftRevision?.RevisionId,
        rfq.Version,
        rfq.PendingDraftRevision?.Version,
        rfq.PendingDraftRevision?.Notional,
        rfq.PendingDraftRevision?.SettlementDate,
        rfq.PendingDraftRevision?.SalesAndTradingMessage,
        rfq.Status,
        rfq.QuoteStatus,
        rfq.QuoteRequestReason);
}
