using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("rfq_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }

    public RfqDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RfqDbContext>();
        DatabaseConfiguration.Configure(options, Container.GetConnectionString());
        return new RfqDbContext(options.Options);
    }
}
