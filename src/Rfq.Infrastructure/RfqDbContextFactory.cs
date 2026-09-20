using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rfq.Infrastructure;

public sealed class RfqDbContextFactory : IDesignTimeDbContextFactory<RfqDbContext>
{
    private const string DevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=rfq;Username=rfq;Password=rfq-dev-password";

    public RfqDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
                $"ConnectionStrings__{DatabaseConfiguration.ConnectionStringName}")
            ?? DevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<RfqDbContext>();
        DatabaseConfiguration.Configure(options, connectionString);
        return new RfqDbContext(options.Options);
    }
}
