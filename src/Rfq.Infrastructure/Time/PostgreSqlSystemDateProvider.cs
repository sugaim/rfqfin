using Microsoft.EntityFrameworkCore;
using Rfq.Application;

namespace Rfq.Infrastructure;

public sealed class PostgreSqlSystemDateProvider(RfqDbContext dbContext) : ISystemDateProvider
{
    public async Task<DateOnly> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        var systemDate = await dbContext.SystemDates
            .AsNoTracking()
            .Where(item => item.Key == DevelopmentDataSeeder.SystemDateKey)
            .Select(item => (DateOnly?)item.BusinessDate)
            .SingleOrDefaultAsync(cancellationToken);
        return systemDate
            ?? throw new InvalidOperationException("The system date is not configured.");
    }
}
