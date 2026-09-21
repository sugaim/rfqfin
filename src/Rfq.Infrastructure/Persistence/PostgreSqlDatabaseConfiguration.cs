using Microsoft.EntityFrameworkCore;

namespace Rfq.Infrastructure;

public static class PostgreSqlDatabaseConfiguration
{
    public const string ConnectionStringName = "RfqDatabase";

    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(RfqDbContext).Assembly.FullName));
    }
}
