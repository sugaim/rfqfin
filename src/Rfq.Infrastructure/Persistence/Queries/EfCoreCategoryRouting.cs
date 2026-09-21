using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreCategoryRouting(
    RfqDbContext dbContext,
    ICurrentUser currentUser) : ICategoryRouting
{
    public async Task<UserId> GetDefaultAssignedTraderAsync(
        CategoryId categoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryId);

        var traderId = await dbContext.CategoryRoutings
            .AsNoTracking()
            .Where(routing => routing.CategoryId == categoryId.Value)
            .Select(routing => routing.DefaultTraderId)
            .SingleOrDefaultAsync(cancellationToken);
        if (traderId is null)
            throw new RfqInvariantException(
                $"No default Assigned Trader is configured for category '{categoryId.Value}'.");
        try { return UserId.Create(traderId); }
        catch (DomainValidationException exception)
        {
            throw new RfqInvariantException(
                "The configured Assigned Trader ID is invalid.", exception);
        }
    }

    public async Task<IReadOnlyList<CategoryRoutingItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await (
                from routing in dbContext.CategoryRoutings.AsNoTracking()
                join category in dbContext.Categories.AsNoTracking()
                    on routing.CategoryId equals category.CategoryId
                join trader in dbContext.MasterUsers.AsNoTracking()
                    on routing.DefaultTraderId equals trader.UserId
                orderby category.Name
                select new CategoryRoutingItem(CategoryId.Create(category.CategoryId), category.Name,
                    UserId.Create(trader.UserId), trader.Name)).ToListAsync(cancellationToken);
        }
        catch (DomainValidationException exception)
        {
            throw new RfqInvariantException("Persisted category routing is invalid.", exception);
        }
    }

    public async Task<CategoryRoutingItem> SetDefaultAssignedTraderAsync(
        CategoryId categoryId,
        UserId traderId,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(
            item => item.CategoryId == categoryId.Value, cancellationToken)
            ?? throw new RfqNotFoundException($"Category '{categoryId.Value}' was not found.");
        var trader = await dbContext.MasterUsers.SingleOrDefaultAsync(
            item => item.UserId == traderId.Value, cancellationToken)
            ?? throw new RfqNotFoundException($"Trader '{traderId.Value}' was not found.");
        if (!trader.Roles.Contains(UserRole.Trader.ToString())
            || trader.DeskId != currentUser.User.DeskId.Value)
            throw new RfqRequestValidationException(
                "Default Assigned Trader must be a Trader on the current user's desk.");
        var routing = await dbContext.CategoryRoutings.SingleOrDefaultAsync(
            item => item.CategoryId == categoryId.Value, cancellationToken)
            ?? throw new RfqInvariantException(
                $"No routing row exists for category '{categoryId.Value}'.");
        routing.DefaultTraderId = trader.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CategoryRoutingItem(categoryId, category.Name, traderId, trader.Name);
    }
}
