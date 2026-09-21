using Rfq.Domain;

namespace Rfq.Application;

public sealed class ResolveRfqCreationContext(
    ISecuritySearch securitySearch,
    ICategoryRouting categoryRouting,
    IUserDirectory userDirectory,
    IBusinessDateProvider businessDateProvider,
    IStandardSettlementResolver settlementResolver,
    ICurrentUser currentUser)
{
    public async Task<RfqCreationContext> ExecuteAsync(
        SecurityId securityId,
        CancellationToken cancellationToken = default)
    {
        var businessDate = await businessDateProvider.GetCurrentAsync(cancellationToken);
        var security = await securitySearch.ResolveAsync(securityId, cancellationToken)
            ?? throw new RfqNotFoundException($"Security '{securityId}' was not found.");
        var categoryId = security.CategoryId;
        var assignedTraderId = await categoryRouting.GetDefaultAssignedTraderAsync(
            categoryId, cancellationToken);
        var assignedTrader = await userDirectory.ResolveAsync(assignedTraderId, cancellationToken)
            ?? throw new RfqInvariantException(
                $"Assigned Trader '{assignedTraderId.Value}' was not found.");
        if (!assignedTrader.Roles.Contains(UserRole.Trader)
            || assignedTrader.DeskId != currentUser.User.DeskId)
        {
            throw new RfqInvariantException(
                "The configured Assigned Trader must be a Trader on the current user's desk.");
        }

        return new RfqCreationContext(
            security.CategoryId,
            security.CategoryName,
            assignedTrader.UserId,
            assignedTrader.Name,
            settlementResolver.Resolve(securityId, businessDate));
    }
}
