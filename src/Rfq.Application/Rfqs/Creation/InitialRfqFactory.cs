using Rfq.Domain;

namespace Rfq.Application;

public sealed class InitialRfqFactory(
    ICaseIdGenerator caseIdGenerator,
    IClientSearch clientSearch,
    ResolveRfqDefaults resolveDefaults,
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
        var defaults = await resolveDefaults.ExecuteAsync(command.SecurityId, cancellationToken);
        var assignedTraderId = await assignedTraderValidator.ResolveAsync(
            command.AssignedTraderId,
            defaults.AssignedTraderId,
            cancellationToken);
        var caseId = await caseIdGenerator.NextAsync(cancellationToken);
        var salesId = currentUser.User.Roles.Contains(UserRole.Sales)
            ? currentUser.User.UserId
            : null;

        return RfqCase.CreateDraft(
            caseId,
            RevisionId.New(),
            command.ClientId,
            defaults.SecurityId,
            defaults.CategoryId,
            assignedTraderId,
            new RevisionTerms(
                command.Notional,
                command.SettlementDate,
                defaults.StandardSettlementDate,
                command.SalesAndTradingMessage),
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            salesId,
            copiedFromCaseId,
            copiedFromRevisionId);
    }
}
