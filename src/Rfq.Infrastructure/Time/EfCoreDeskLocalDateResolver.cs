using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreDeskLocalDateResolver(RfqDbContext dbContext)
    : IDeskLocalDateResolver
{
    public async Task<DateOnly> ResolveAsync(
        DateTimeOffset instant,
        DeskId deskId,
        CancellationToken cancellationToken = default)
    {
        string timeZoneId = await dbContext.Desks.AsNoTracking()
            .Where(item => item.DeskId == deskId.Value)
            .Select(item => item.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RfqInvariantException($"Desk '{deskId.Value}' was not found.");
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);
    }
}
