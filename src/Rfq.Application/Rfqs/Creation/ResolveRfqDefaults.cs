using Rfq.Domain;

namespace Rfq.Application;

public sealed class ResolveRfqDefaults(
    ISecuritySearch securitySearch,
    ICategoryRouting categoryRouting,
    IUserDirectory userDirectory,
    ISystemDateProvider systemDateProvider,
    IStandardSettlementResolver settlementResolver,
    ICurrentUser currentUser)
{
    public async Task<RfqDefaultsResult> ExecuteAsync(
        string securityId,
        CancellationToken cancellationToken = default)
    {
        var resolvedSecurityId = SecurityId.Create(securityId);
        var systemDate = await systemDateProvider.GetTodayAsync(cancellationToken);
        var security = await securitySearch.ResolveAsync(resolvedSecurityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Security '{securityId}' was not found.");
        var categoryId = CategoryId.Create(security.CategoryId);
        var assignedTraderId = await categoryRouting.GetDefaultAssignedTraderAsync(
                categoryId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"No default Assigned Trader is configured for category '{categoryId.Value}'.");
        var assignedTrader = await userDirectory.ResolveAsync(assignedTraderId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Assigned Trader '{assignedTraderId.Value}' was not found.");
        var contactOwner = await userDirectory.ResolveAsync(
                currentUser.User.UserId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Current user '{currentUser.User.UserId.Value}' was not found.");
        if (!assignedTrader.Roles.Contains(UserRole.Trader)
            || !string.Equals(assignedTrader.DeskId, currentUser.User.DeskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The configured Assigned Trader must be a Trader on the current user's desk.");
        }

        return new RfqDefaultsResult(
            security.SecurityId,
            security.CategoryId,
            security.CategoryName,
            contactOwner.UserId,
            contactOwner.Name,
            assignedTrader.UserId,
            assignedTrader.Name,
            systemDate,
            settlementResolver.Resolve(resolvedSecurityId, systemDate));
    }
}
