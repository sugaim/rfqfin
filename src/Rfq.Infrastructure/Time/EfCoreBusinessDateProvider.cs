using Microsoft.EntityFrameworkCore;
using Rfq.Application;

namespace Rfq.Infrastructure;

public sealed class EfCoreBusinessDateProvider(RfqDbContext dbContext) : IBusinessDateProvider
{
    public async Task<DateOnly> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var businessDate = await dbContext.BusinessDates
            .AsNoTracking()
            .Where(item => item.Key == DevelopmentDataSeeder.BusinessDateKey)
            .Select(item => (DateOnly?)item.BusinessDate)
            .SingleOrDefaultAsync(cancellationToken);
        return businessDate
            ?? throw new InvalidOperationException("The business date is not configured.");
    }
}
