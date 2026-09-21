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
        rfqCase.UpdateInitialDraft(
            command.Notional,
            command.SettlementDate,
            command.SalesAndTradingMessage,
            assignedTraderId,
            command.ExpectedVersion);

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
    IWorkingQuoteEnsurer workingQuoteEnsurer,
    ISystemDateProvider systemDateProvider,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
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
        rfqCase.UpdateAndConfirmInitial(
            command.Notional,
            command.SettlementDate,
            command.SalesAndTradingMessage,
            assignedTraderId,
            systemDate,
            currentUser.User.UserId,
            now,
            command.ExpectedVersion);

        rfqCases.Update(rfqCase);
        await workingQuoteEnsurer.EnsureAsync(
            rfqCase.InitialRevision.RevisionId,
            rfqCase.InitialRevision.QuoteSeedRevisionId,
            currentUser.User.UserId,
            now,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(rfqCase);
    }
}

public sealed class ConfirmNewRfq(
    InitialRfqFactory initialRfqFactory,
    IRfqCaseRepository rfqCases,
    IWorkingQuoteEnsurer workingQuoteEnsurer,
    ISystemDateProvider systemDateProvider,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        CreateDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanCreateRevision(currentUser.User);
        var rfqCase = await initialRfqFactory.CreateAsync(command, cancellationToken);
        var systemDate = await systemDateProvider.GetTodayAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        rfqCase.ConfirmInitial(
            systemDate,
            currentUser.User.UserId,
            now,
            rfqCase.InitialRevision.Version);

        rfqCases.Add(rfqCase);
        await workingQuoteEnsurer.EnsureAsync(
            rfqCase.InitialRevision.RevisionId,
            rfqCase.InitialRevision.QuoteSeedRevisionId,
            currentUser.User.UserId,
            now,
            cancellationToken);
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
        rfqCase.DiscardInitialDraft(expectedVersion);
        rfqCases.Update(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
