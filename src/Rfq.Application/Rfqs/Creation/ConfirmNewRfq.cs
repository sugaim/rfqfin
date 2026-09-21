using Rfq.Domain;

namespace Rfq.Application;

public sealed class ConfirmNewRfq(
    InitialRfqFactory initialRfqFactory,
    IRfqCaseRepository rfqCases,
    IWorkingQuoteRepository workingQuotes,
    ISystemDateProvider systemDateProvider,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IRfqEventSink eventSink)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        CreateDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanCreateRevision(currentUser.User);
        var rfqCase = await initialRfqFactory.CreateAsync(command, cancellationToken);
        var systemDate = await systemDateProvider.GetTodayAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        rfqCase = RfqLifecycleTransitions.ConfirmInitial(
            rfqCase, rfqCase.CurrentRevision.Terms, rfqCase.AssignedTraderId,
            systemDate, currentUser.User.UserId, now, rfqCase.CurrentRevision.Version);

        rfqCases.Add(rfqCase);
        workingQuotes.Add(WorkingQuoteFactory.CreateInitialFor(
            rfqCase, currentUser.User.UserId, now));
        eventSink.Record(new RfqTransition(
            RfqTransitionKind.RevisionConfirmed,
            rfqCase.CaseId,
            currentUser.User.UserId,
            now,
            To: rfqCase.CurrentRevision.RevisionId.Value.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(rfqCase);
    }
}
