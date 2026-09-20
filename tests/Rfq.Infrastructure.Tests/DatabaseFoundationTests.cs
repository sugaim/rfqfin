using Microsoft.EntityFrameworkCore;
using Rfq.Domain;
using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class DatabaseFoundationTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task MigrationsApplyFromEmptyPostgreSqlDatabase()
    {
        await using var context = fixture.CreateContext();
        await context.Database.EnsureDeletedAsync();

        await context.Database.MigrateAsync();

        Assert.True(await context.Database.CanConnectAsync());
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains(
            appliedMigrations,
            migration => migration.EndsWith("_InitialDatabaseFoundation", StringComparison.Ordinal));
        Assert.Equal(0, await context.SeedMarkers.CountAsync());
    }

    [Fact]
    public async Task DevelopmentSeedIsIdempotent()
    {
        await RecreateDatabaseAsync();

        await using (var firstContext = fixture.CreateContext())
        {
            var firstSeeder = new DevelopmentDataSeeder(firstContext, TimeProvider.System);
            await firstSeeder.SeedAsync();
        }

        await using (var secondContext = fixture.CreateContext())
        {
            var secondSeeder = new DevelopmentDataSeeder(secondContext, TimeProvider.System);
            await secondSeeder.SeedAsync();
            Assert.Equal(1, await secondContext.SeedMarkers.CountAsync());
        }
    }

    [Fact]
    public async Task ResetDevGuardRejectsProductionBeforeChangingDatabase()
    {
        await RecreateDatabaseAsync();

        await using var context = fixture.CreateContext();
        var seeder = new DevelopmentDataSeeder(context, TimeProvider.System);
        var operations = new DatabaseOperations(context, seeder);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => operations.ResetDevelopmentAsync("Production"));

        Assert.True(await context.Database.CanConnectAsync());
        Assert.Equal(0, await context.SeedMarkers.CountAsync());
    }

    [Fact]
    public async Task CreateAndReadDraftRoundTripsThroughPostgreSql()
    {
        await RecreateDatabaseAsync();
        var salesUserId = UserId.Create("sales-roundtrip");
        var createdAt = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

        await using (var writeContext = fixture.CreateContext())
        {
            var repository = new RfqCaseRepository(writeContext);
            var unitOfWork = new EfUnitOfWork(writeContext);
            repository.Add(RfqCase.CreateDraft(
                ClientId.Create("client-roundtrip"),
                SecurityId.Create("security-roundtrip"),
                salesUserId,
                createdAt));

            await unitOfWork.SaveChangesAsync();
        }

        await using (var readContext = fixture.CreateContext())
        {
            var repository = new RfqCaseRepository(readContext);
            var rows = await repository.GetActiveSalesRfqsAsync(salesUserId);

            var row = Assert.Single(rows);
            Assert.Equal("client-roundtrip", row.ClientId);
            Assert.Equal("security-roundtrip", row.SecurityId);
            Assert.Equal("Draft", row.RfqStatus);
            Assert.Equal("Draft", row.RevisionStatus);
            Assert.Equal(createdAt, row.CreatedAt);
        }
    }

    private async Task RecreateDatabaseAsync()
    {
        await using var context = fixture.CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }
}
