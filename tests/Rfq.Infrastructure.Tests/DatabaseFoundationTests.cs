using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        long caseId;

        await using (var writeContext = fixture.CreateContext())
        {
            var caseIdGenerator = new PostgreSqlCaseIdGenerator(writeContext);
            var repository = new RfqCaseRepository(writeContext);
            var unitOfWork = new EfUnitOfWork(writeContext);
            caseId = (await caseIdGenerator.NextAsync()).Value;
            repository.Add(RfqCase.CreateDraft(
                new CaseId(caseId),
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
            Assert.Equal(caseId, row.CaseId);
            Assert.Equal("client-roundtrip", row.ClientId);
            Assert.Equal("security-roundtrip", row.SecurityId);
            Assert.Equal("Draft", row.RfqStatus);
            Assert.Equal("Draft", row.RevisionStatus);
            Assert.Equal(createdAt, row.CreatedAt);
        }
    }

    [Fact]
    public async Task CaseIdSequenceReturnsIncrementingIntegers()
    {
        await RecreateDatabaseAsync();

        await using var context = fixture.CreateContext();
        var generator = new PostgreSqlCaseIdGenerator(context);

        var first = await generator.NextAsync();
        var second = await generator.NextAsync();

        Assert.Equal(first.Value + 1, second.Value);
    }

    [Fact]
    public async Task IncrementalCaseIdMigrationPreservesExistingRelationships()
    {
        var firstCaseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondCaseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var firstRevisionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondRevisionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var firstCreatedAt = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var secondCreatedAt = firstCreatedAt.AddMinutes(1);

        await using (var upgradeContext = fixture.CreateContext())
        {
            await upgradeContext.Database.EnsureDeletedAsync();
            var migrator = upgradeContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260920161234_AddRfqDrafts");

            await upgradeContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO rfq_cases
                    (case_id, client_id, security_id, created_at, created_by, sales_id)
                VALUES
                    ({firstCaseId}, 'client-old-1', 'security-old-1', {firstCreatedAt}, 'sales-upgrade', 'sales-upgrade'),
                    ({secondCaseId}, 'client-old-2', 'security-old-2', {secondCreatedAt}, 'sales-upgrade', 'sales-upgrade');

                INSERT INTO rfq_revisions
                    (revision_id, case_id, status, version, created_at, created_by)
                VALUES
                    ({firstRevisionId}, {firstCaseId}, 'Draft', 1, {firstCreatedAt}, 'sales-upgrade'),
                    ({secondRevisionId}, {secondCaseId}, 'Draft', 1, {secondCreatedAt}, 'sales-upgrade');

                INSERT INTO case_currents
                    (case_id, lifecycle, rfq_status, current_revision_id, version)
                VALUES
                    ({firstCaseId}, 'Draft', 'Draft', {firstRevisionId}, 1),
                    ({secondCaseId}, 'Draft', 'Draft', {secondRevisionId}, 1);
                """);

            await migrator.MigrateAsync();
        }

        await using var readContext = fixture.CreateContext();
        var repository = new RfqCaseRepository(readContext);
        var rows = await repository.GetActiveSalesRfqsAsync(UserId.Create("sales-upgrade"));

        Assert.Collection(
            rows.OrderBy(row => row.CaseId),
            first =>
            {
                Assert.Equal(1, first.CaseId);
                Assert.Equal("client-old-1", first.ClientId);
                Assert.Equal(firstRevisionId, first.CurrentRevisionId);
            },
            second =>
            {
                Assert.Equal(2, second.CaseId);
                Assert.Equal("client-old-2", second.ClientId);
                Assert.Equal(secondRevisionId, second.CurrentRevisionId);
            });

        var nextCaseId = await new PostgreSqlCaseIdGenerator(readContext).NextAsync();
        Assert.Equal(3, nextCaseId.Value);
    }

    private async Task RecreateDatabaseAsync()
    {
        await using var context = fixture.CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }
}
