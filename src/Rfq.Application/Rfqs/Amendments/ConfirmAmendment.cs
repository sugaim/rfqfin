using Rfq.Domain;

namespace Rfq.Application;

public sealed class ConfirmAmendment(
    IRfqCaseRepository cases,
    IWorkingQuoteRepository workingQuotes,
    ISystemDateProvider systemDate,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<AmendmentResult> ExecuteAsync(
        AmendmentItem command,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, command.CaseId, cancellationToken);
        authorization.EnsureCanConfirmRevision(currentUser.User, rfq);
        var now = timeProvider.GetUtcNow();
        var transition = AmendmentTransitions.Confirm(
            rfq,
            await systemDate.GetTodayAsync(cancellationToken),
            currentUser.User.UserId,
            now,
            command.ExpectedCurrentVersion,
            command.ExpectedDraftVersion);
        rfq = transition.Rfq;
        cases.Update(rfq);
        cases.UpdateRevision(transition.SupersededRevision);
        var seed = rfq.CurrentRevision.QuoteSeedRevisionId is null
            ? null
            : await workingQuotes.GetAsync(
                rfq.CurrentRevision.QuoteSeedRevisionId.Value, cancellationToken);
        workingQuotes.Add(WorkingQuoteFactory.CreateForAmendment(
            rfq, seed, currentUser.User.UserId, now));
        events.Record(new RfqTransition(
            RfqTransitionKind.RevisionConfirmed,
            rfq.CaseId,
            currentUser.User.UserId,
            now,
            From: transition.SupersededRevision.RevisionId.Value.ToString(),
            To: rfq.CurrentRevision.RevisionId.Value.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SaveAmendment.ToResult(rfq);
    }
}
