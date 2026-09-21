using Rfq.Domain;

namespace Rfq.Application;

public sealed record UpdateInitialDraftCommand(
    long CaseId,
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    string? AssignedTraderId,
    long ExpectedVersion);

public sealed class UpdateInitialDraft(
    IRfqCaseRepository rfqCases,
    AssignedTraderValidator assignedTraderValidator,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        UpdateInitialDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await GetCaseAsync(
            rfqCases,
            command.CaseId,
            cancellationToken);
        authorization.EnsureCanEditRevision(currentUser.User, rfqCase);
        var assignedTraderId = await assignedTraderValidator.ResolveAsync(
            command.AssignedTraderId,
            rfqCase.AssignedTraderId.Value,
            cancellationToken);
        rfqCase = InitialDraftTransitions.Update(
            rfqCase,
            new RevisionTerms(command.Notional, command.SettlementDate,
                rfqCase.CurrentRevision.StandardSettlementDate,
                command.SalesAndTradingMessage),
            assignedTraderId,
            new StateVersion(command.ExpectedVersion));

        rfqCases.Update(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(rfqCase);
    }

    internal static async Task<RfqCase> GetCaseAsync(
        IRfqCaseRepository rfqCases,
        long caseId,
        CancellationToken cancellationToken)
    {
        var rfqCase = await rfqCases.GetAsync(new CaseId(caseId), cancellationToken)
            ?? throw new KeyNotFoundException($"RFQ Case '{caseId}' was not found.");
        return rfqCase;
    }
}

public sealed class ConfirmInitialDraft(
    IRfqCaseRepository rfqCases,
    AssignedTraderValidator assignedTraderValidator,
    IWorkingQuoteRepository workingQuotes,
    ISystemDateProvider systemDateProvider,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IRfqEventSink? eventSink = null)
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
            rfqCase.AssignedTraderId.Value,
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
            new StateVersion(command.ExpectedVersion));

        rfqCases.Update(rfqCase);
        workingQuotes.Add(WorkingQuoteFactory.CreateInitialFor(
            rfqCase, currentUser.User.UserId, now));
        eventSink?.Record(new RfqTransition(
            RfqTransitionKind.RevisionConfirmed,
            rfqCase.CaseId.Value,
            currentUser.User.UserId.Value,
            now,
            To: rfqCase.CurrentRevision.RevisionId.Value.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(rfqCase);
    }
}

public sealed class ConfirmNewRfq(
    InitialRfqFactory initialRfqFactory,
    IRfqCaseRepository rfqCases,
    IWorkingQuoteRepository workingQuotes,
    ISystemDateProvider systemDateProvider,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IRfqEventSink? eventSink = null)
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
        eventSink?.Record(new RfqTransition(
            RfqTransitionKind.RevisionConfirmed,
            rfqCase.CaseId.Value,
            currentUser.User.UserId.Value,
            now,
            To: rfqCase.CurrentRevision.RevisionId.Value.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(rfqCase);
    }
}

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
