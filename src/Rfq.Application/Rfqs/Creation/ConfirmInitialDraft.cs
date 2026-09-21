using Rfq.Domain;

namespace Rfq.Application;

public sealed class ConfirmInitialDraft(
    IRfqCaseRepository rfqCases,
    AssignedTraderValidator assignedTraderValidator,
    IWorkingQuoteRepository workingQuotes,
    ISystemDateProvider systemDateProvider,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IRfqEventSink eventSink)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        UpdateInitialDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await UpdateInitialDraft.GetCaseAsync(
            rfqCases,
            command.CaseId,
            cancellationToken);
        authorization.EnsureCanConfirmRevision(currentUser.User, rfqCase);
        var assignedTraderId = await assignedTraderValidator.ResolveAsync(
            command.AssignedTraderId,
            rfqCase.AssignedTraderId,
            cancellationToken);
        var systemDate = await systemDateProvider.GetTodayAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        rfqCase = RfqLifecycleTransitions.ConfirmInitial(
            rfqCase,
            new RevisionTerms(command.Notional, command.SettlementDate,
                rfqCase.CurrentRevision.StandardSettlementDate,
                command.SalesAndTradingMessage),
            assignedTraderId,
            systemDate,
            currentUser.User.UserId,
            now,
            command.ExpectedVersion);

        rfqCases.Update(rfqCase);
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
