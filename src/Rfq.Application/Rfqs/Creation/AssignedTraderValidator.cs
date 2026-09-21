using Rfq.Domain;

namespace Rfq.Application;

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
