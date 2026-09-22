using Rfq.Domain;

namespace Rfq.Application;

public sealed class ConfirmInitialDraft(
    IRfqCaseRepository rfqCases,
    AssignedTraderValidator assignedTraderValidator,
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
        UpdateInitialDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfqCase = await UpdateInitialDraft.GetCaseAsync(
            rfqCases,
            command.CaseId,
            cancellationToken);
        authorization.EnsureCanConfirmRevision(currentUser.User, rfqCase);
        UserId assignedTraderId = await assignedTraderValidator.ResolveAsync(
            command.AssignedTraderId,
            cancellationToken);
        if (command.StandardSettlementDate != rfqCase.CurrentRevision.StandardSettlementDate)
        {
            throw new RfqRequestValidationException(
                "Standard Settlement Date cannot differ from the RFQ creation context.");
        }

        DateOnly businessDate = await businessDateProvider.GetCurrentAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        rfqCase = RfqLifecycleTransitions.ConfirmInitial(
            rfqCase,
            new RevisionTerms(
                command.Notional,
                command.SettlementDate,
                command.StandardSettlementDate,
                command.SalesAndTradingMessage),
            assignedTraderId,
            businessDate,
            currentUser.User.UserId,
            now,
            command.ExpectedVersion);

        rfqCases.Update(rfqCase);
        WorkingQuoteMode defaultMode = quoteModeSettings is null
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
