using Rfq.Domain;

namespace Rfq.Application;

public sealed class AssignedTraderValidator(
    IUserDirectory userDirectory,
    ICurrentUser currentUser)
{
    public async Task<UserId> ResolveAsync(
        UserId assignedTraderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignedTraderId);
        var assignedTrader = await userDirectory.ResolveAsync(assignedTraderId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Assigned Trader '{assignedTraderId.Value}' was not found.");
        if (!assignedTrader.Roles.Contains(UserRole.Trader)
            || assignedTrader.DeskId != currentUser.User.DeskId)
        {
            throw new ArgumentException(
                "Assigned Trader must be a Trader on the current user's desk.",
                nameof(assignedTraderId));
        }

        return assignedTraderId;
    }
}
