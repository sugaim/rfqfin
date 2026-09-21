using Microsoft.EntityFrameworkCore;

namespace Rfq.Infrastructure;

public sealed class DatabaseOperations(
    RfqDbContext dbContext,
    DevelopmentDataSeeder dataSeeder)
{
    public Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return dataSeeder.SeedAsync(cancellationToken);
    }

    public async Task ResetDevelopmentAsync(
        string environmentName,
        CancellationToken cancellationToken = default)
    {
        var connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The RFQ database connection string is missing.");

        PostgreSqlResetDevSafetyGuard.EnsureAllowed(environmentName, connectionString);

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        await dataSeeder.SeedAsync(cancellationToken);
    }
}
