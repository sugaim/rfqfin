using Rfq.Domain;

namespace Rfq.Application;

public sealed class InitialRfqFactory(
    ICaseIdGenerator caseIdGenerator,
    IClientSearch clientSearch,
    ResolveRfqCreationContext resolveCreationContext,
    AssignedTraderValidator assignedTraderValidator,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<RfqCase> CreateAsync(
        CreateDraftCommand command,
        CancellationToken cancellationToken = default,
        CaseId? copiedFromCaseId = null,
        RevisionId? copiedFromRevisionId = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        _ = await clientSearch.ResolveAsync(command.ClientId, cancellationToken)
            ?? throw new KeyNotFoundException($"Client '{command.ClientId.Value}' was not found.");
        var context = await resolveCreationContext.ExecuteAsync(command.SecurityId, cancellationToken);
        if (command.StandardSettlementDate != context.StandardSettlementDate)
            throw new ArgumentException(
                "Standard Settlement Date no longer matches the authoritative creation context.",
                nameof(command));
        var assignedTraderId = await assignedTraderValidator.ResolveAsync(
            command.AssignedTraderId,
            cancellationToken);
        var caseId = await caseIdGenerator.NextAsync(cancellationToken);
        var salesId = currentUser.User.Roles.Contains(UserRole.Sales)
            ? currentUser.User.UserId
            : null;

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
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            salesId,
            copiedFromCaseId,
            copiedFromRevisionId);
    }
}
