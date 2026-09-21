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

        var clientId = ClientId.Create(command.ClientId);
        _ = await clientSearch.ResolveAsync(clientId, cancellationToken)
            ?? throw new KeyNotFoundException($"Client '{clientId.Value}' was not found.");
        var defaults = await resolveDefaults.ExecuteAsync(command.SecurityId, cancellationToken);
        var assignedTraderId = await assignedTraderValidator.ResolveAsync(
            command.AssignedTraderId,
            defaults.AssignedTraderId,
            cancellationToken);
        var caseId = await caseIdGenerator.NextAsync(cancellationToken);

        return RfqCase.CreateDraft(
            caseId,
            clientId,
            SecurityId.Create(defaults.SecurityId),
            CategoryId.Create(defaults.CategoryId),
            assignedTraderId,
            command.Notional,
            command.SettlementDate,
            defaults.StandardSettlementDate,
            command.SalesAndTradingMessage,
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            copiedFromCaseId,
            copiedFromRevisionId);
    }
}

public sealed class AssignedTraderValidator(
    IUserDirectory userDirectory,
    ICurrentUser currentUser)
{
    public async Task<UserId> ResolveAsync(
        string? requestedTraderId,
        string fallbackTraderId,
        CancellationToken cancellationToken = default)
    {
        var assignedTraderId = UserId.Create(
            string.IsNullOrWhiteSpace(requestedTraderId)
                ? fallbackTraderId
                : requestedTraderId);
        var assignedTrader = await userDirectory.ResolveAsync(assignedTraderId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Assigned Trader '{assignedTraderId.Value}' was not found.");
        if (!assignedTrader.Roles.Contains(UserRole.Trader)
            || !string.Equals(
                assignedTrader.DeskId,
                currentUser.User.DeskId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Assigned Trader must be a Trader on the current user's desk.",
                nameof(requestedTraderId));
        }

        return assignedTraderId;
    }
}
