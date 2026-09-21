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
        var rfq = await CloseRfq.LoadAsync(cases, command.CaseId, cancellationToken);
        authorization.EnsureCanEditRevision(currentUser.User, rfq);
        var transition = AmendmentTransitions.SaveDraft(
            rfq,
            RevisionId.New(),
            new RevisionTerms(command.Notional, command.SettlementDate,
                rfq.CurrentRevision.StandardSettlementDate,
                command.SalesAndTradingMessage),
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            new StateVersion(command.ExpectedCurrentVersion),
            command.ExpectedDraftVersion is null
                ? null : new StateVersion(command.ExpectedDraftVersion.Value));
        rfq = transition.Rfq;
        cases.Update(rfq);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(rfq);
    }

    internal static AmendmentResult ToResult(RfqCase rfq) => new(
        rfq.CaseId.Value,
        rfq.CurrentRevision.RevisionId.Value,
        rfq.PendingDraftRevision?.RevisionId.Value,
        rfq.Version.Value,
        rfq.PendingDraftRevision?.Version.Value,
        rfq.Status.ToString(),
        rfq.QuoteStatus?.ToString(),
        rfq.QuoteRequestReason?.ToString());
}
