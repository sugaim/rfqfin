using Rfq.Domain;

namespace Rfq.Application;

public sealed class ConfirmNewRfq(
    InitialRfqFactory initialRfqFactory,
    IRfqCaseRepository rfqCases,
    IWorkingQuoteRepository workingQuotes,
    IBusinessDateProvider businessDateProvider,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IRfqEventSink eventSink,
    IQuoteModeSettings? quoteModeSettings = null)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        CreateDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanCreateRevision(currentUser.User);
        var rfqCase = await initialRfqFactory.CreateAsync(command, cancellationToken);
        var businessDate = await businessDateProvider.GetCurrentAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        rfqCase = RfqLifecycleTransitions.ConfirmInitial(
            rfqCase, rfqCase.CurrentRevision.Terms, rfqCase.AssignedTraderId,
            businessDate, currentUser.User.UserId, now, rfqCase.CurrentRevision.Version);

        rfqCases.Add(rfqCase);
        var defaultMode = quoteModeSettings is null
            ? WorkingQuoteMode.Calculated
            : await quoteModeSettings.GetAsync(rfqCase.AssignedTraderId, cancellationToken);
        workingQuotes.Add(WorkingQuoteFactory.CreateInitialFor(
            rfqCase, currentUser.User.UserId, now, defaultMode));
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
