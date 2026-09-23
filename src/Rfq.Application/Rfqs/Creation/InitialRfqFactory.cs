using Rfq.Domain;

namespace Rfq.Application;

public sealed class InitialRfqFactory(
    ICaseIdGenerator caseIdGenerator,
    IClientSearch clientSearch,
    ResolveRfqCreationContext resolveCreationContext,
    AssignedTraderValidator assignedTraderValidator,
    ICurrentUser currentUser,
    IBusinessDateProvider businessDateProvider,
    TimeProvider timeProvider)
{
    public async Task<RfqCase> CreateAsync(
        CreateDraftCommand command,
        CaseId? copiedFromCaseId = null,
        RevisionId? copiedFromRevisionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _ = await clientSearch.ResolveAsync(command.ClientId, cancellationToken)
            ?? throw new RfqNotFoundException($"Client '{command.ClientId.Value}' was not found.");
        RfqCreationContext context = await resolveCreationContext.ExecuteAsync(command.SecurityId, cancellationToken);
        if (command.StandardSettlementDate != context.StandardSettlementDate)
        {
            throw new RfqRequestValidationException(
                "Standard Settlement Date no longer matches the authoritative creation context.");
        }

        UserId assignedTraderId = await assignedTraderValidator.ResolveAsync(
            command.AssignedTraderId,
            cancellationToken);
        CaseId caseId = await caseIdGenerator.NextAsync(cancellationToken);
        UserId? salesId = currentUser.User.Roles.Contains(UserRole.Sales)
            ? currentUser.User.UserId
            : null;
        DateOnly businessDate = await businessDateProvider.GetCurrentAsync(cancellationToken);

        return RfqCase.CreateDraft(
            caseId,
            RevisionId.New(),
            command.ClientId,
            command.SecurityId,
            context.CategoryId,
            assignedTraderId,
            new RevisionTerms(
                command.Notional,
                command.SettlementDate,
                command.StandardSettlementDate,
                command.SalesAndTradingMessage),
            businessDate,
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            salesId,
            copiedFromCaseId,
            copiedFromRevisionId);
    }
}
