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
        DateOnly businessDate = await businessDateProvider.GetCurrentAsync(cancellationToken);
        SecuritySearchResult security = await securitySearch.ResolveAsync(securityId, cancellationToken)
            ?? throw new RfqNotFoundException($"Security '{securityId}' was not found.");
        CategoryId categoryId = security.CategoryId;
        UserId assignedTraderId = await categoryRouting.GetDefaultAssignedTraderAsync(
            categoryId, cancellationToken);
        UserSummary assignedTrader = await userDirectory.ResolveAsync(assignedTraderId, cancellationToken)
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
