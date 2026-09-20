using Microsoft.EntityFrameworkCore;

namespace Rfq.Infrastructure;

public sealed class DevelopmentDataSeeder(RfqDbContext dbContext, TimeProvider timeProvider)
{
    public const string FoundationSeedKey = "database-foundation-v1";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.SeedMarkers.AnyAsync(
                marker => marker.Key == FoundationSeedKey,
                cancellationToken))
        {
            return;
        }

        dbContext.SeedMarkers.Add(
            new SeedMarker(FoundationSeedKey, timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
