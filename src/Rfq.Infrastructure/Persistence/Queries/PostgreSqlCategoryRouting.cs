using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class PostgreSqlCategoryRouting(RfqDbContext dbContext) : ICategoryRouting
{
    public async Task<UserId?> GetDefaultAssignedTraderAsync(
        CategoryId categoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryId);

        var traderId = await dbContext.CategoryRoutings
            .AsNoTracking()
            .Where(routing => routing.CategoryId == categoryId.Value)
            .Select(routing => routing.DefaultTraderId)
            .SingleOrDefaultAsync(cancellationToken);
        return traderId is null ? null : UserId.Create(traderId);
    }
}
