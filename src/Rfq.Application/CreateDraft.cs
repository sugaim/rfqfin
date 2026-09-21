using Rfq.Domain;

namespace Rfq.Application;

public sealed record CreateDraftCommand(
    string ClientId,
    string SecurityId,
    DateOnly SettlementDate,
    string? AssignedTraderId);

public sealed record CreateDraftResult(
    long CaseId,
    Guid RevisionId,
    string RfqStatus,
    string CategoryId,
    string ContactOwnerId,
    string AssignedTraderId,
    DateOnly SettlementDate,
    DateOnly StandardSettlementDate,
    DateTimeOffset CreatedAt);

public sealed class CreateDraft(
    ICaseIdGenerator caseIdGenerator,
    IRfqCaseRepository rfqCases,
    IUnitOfWork unitOfWork,
    IClientSearch clientSearch,
    IUserDirectory userDirectory,
    ResolveRfqDefaults resolveDefaults,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<CreateDraftResult> ExecuteAsync(
        CreateDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var clientId = ClientId.Create(command.ClientId);
        _ = await clientSearch.ResolveAsync(clientId, cancellationToken)
            ?? throw new KeyNotFoundException($"Client '{clientId.Value}' was not found.");
        if (command.SettlementDate == default)
        {
            throw new ArgumentException("Settlement date is required.", nameof(command));
        }

        var defaults = await resolveDefaults.ExecuteAsync(
            command.SecurityId,
            cancellationToken);
        var assignedTraderId = UserId.Create(
            string.IsNullOrWhiteSpace(command.AssignedTraderId)
                ? defaults.AssignedTraderId
                : command.AssignedTraderId);
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
                nameof(command));
        }

        var caseId = await caseIdGenerator.NextAsync(cancellationToken);
        var rfqCase = RfqCase.CreateDraft(
            caseId,
            clientId,
            SecurityId.Create(defaults.SecurityId),
            CategoryId.Create(defaults.CategoryId),
            assignedTraderId,
            command.SettlementDate,
            defaults.StandardSettlementDate,
            currentUser.User.UserId,
            timeProvider.GetUtcNow());

        rfqCases.Add(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateDraftResult(
            rfqCase.CaseId.Value,
            rfqCase.InitialRevision.RevisionId.Value,
            rfqCase.Status.ToString(),
            rfqCase.CategorySnapshot.Value,
            rfqCase.ContactOwnerId.Value,
            rfqCase.AssignedTraderId.Value,
            rfqCase.InitialRevision.SettlementDate,
            rfqCase.InitialRevision.StandardSettlementDate,
            rfqCase.CreatedAt);
    }
}
