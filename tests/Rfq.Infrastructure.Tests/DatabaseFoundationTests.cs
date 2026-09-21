using System.Data.Common;
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
            Assert.Equal(3, await secondContext.SeedMarkers.CountAsync());
            Assert.Equal(
                4,
                (await new PostgreSqlClientSearch(secondContext).SearchAsync("C")).Count);
            Assert.Single(
                await new PostgreSqlSecuritySearch(secondContext).SearchAsync("375-1"));
            Assert.Equal(
                new DateOnly(2026, 9, 21),
                await new PostgreSqlSystemDateProvider(secondContext).GetTodayAsync());
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
                CategoryId.Create("JGB"),
                UserId.Create("trader-roundtrip"),
                100_000_000m,
                new DateOnly(2026, 9, 24),
                new DateOnly(2026, 9, 23),
                "Roundtrip message",
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
            Assert.Equal("client-roundtrip", row.ClientName);
            Assert.Equal("security-roundtrip", row.SecurityId);
            Assert.Equal("security-roundtrip", row.SecurityJapaneseName);
            Assert.Equal("Draft", row.RfqStatus);
            Assert.Equal("Draft", row.RevisionStatus);
            Assert.Equal("JGB", row.CategoryId);
            Assert.Equal("trader-roundtrip", row.AssignedTraderId);
            Assert.Equal(new DateOnly(2026, 9, 24), row.SettlementDate);
            Assert.Equal(100_000_000m, row.Notional);
            Assert.Equal("Roundtrip message", row.SalesAndTradingMessage);
            Assert.Equal(createdAt, row.CreatedAt);
        }
    }

    [Fact]
    public async Task ConfirmedCurrentRevisionHasExactlyOneWorkingQuote()
    {
        await RecreateDatabaseAsync();
        var salesUserId = UserId.Create("sales-confirm");
        var confirmedAt = new DateTimeOffset(2026, 9, 21, 1, 0, 0, TimeSpan.Zero);
        Guid revisionId;
        long caseId;

        await using (var createContext = fixture.CreateContext())
        {
            var repository = new RfqCaseRepository(createContext);
            caseId = (await new PostgreSqlCaseIdGenerator(createContext).NextAsync()).Value;
            var rfqCase = RfqCase.CreateDraft(
                new CaseId(caseId),
                ClientId.Create("client-confirm"),
                SecurityId.Create("security-confirm"),
                CategoryId.Create("JGB"),
                UserId.Create("trader-confirm"),
                100_000_000m,
                new DateOnly(2026, 9, 24),
                new DateOnly(2026, 9, 23),
                "Confirm roundtrip",
                salesUserId,
                confirmedAt.AddMinutes(-1));
            revisionId = rfqCase.InitialRevision.RevisionId.Value;
            repository.Add(rfqCase);
            await createContext.SaveChangesAsync();
        }

        await using (var confirmContext = fixture.CreateContext())
        {
            var repository = new RfqCaseRepository(confirmContext);
            var rfqCase = Assert.IsType<RfqCase>(
                await repository.GetAsync(new CaseId(caseId)));
            rfqCase.ConfirmInitial(
                new DateOnly(2026, 9, 21),
                salesUserId,
                confirmedAt,
                rfqCase.InitialRevision.Version);
            repository.Update(rfqCase);
            var workingQuotes = new WorkingQuoteEnsurer(confirmContext);
            await workingQuotes.EnsureAsync(
                rfqCase.InitialRevision.RevisionId,
                null,
                salesUserId,
                confirmedAt);
            await workingQuotes.EnsureAsync(
                rfqCase.InitialRevision.RevisionId,
                null,
                salesUserId,
                confirmedAt);
            await confirmContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var row = Assert.Single(
            await new RfqCaseRepository(readContext)
                .GetActiveSalesRfqsAsync(salesUserId));
        Assert.Equal("Active", row.RfqStatus);
        Assert.Equal("Confirmed", row.RevisionStatus);
        Assert.Equal("Requested", row.QuoteStatus);
        Assert.Equal("Initial", row.QuoteRequestReason);
        Assert.Equal(revisionId, row.CurrentRevisionId);
        var workingQuoteCount = await readContext.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM working_quotes
                WHERE revision_id = {revisionId}
                """)
            .SingleAsync();
        Assert.Equal(1, workingQuoteCount);
    }

    [Fact]
    public async Task EnsureWorkingQuoteReturnsExistingAndClonesConfiguredSeed()
    {
        await RecreateDatabaseAsync();
        var user = UserId.Create("sales-quote-seed");
        var now = new DateTimeOffset(2026, 9, 21, 2, 0, 0, TimeSpan.Zero);
        RevisionId seedRevisionId;
        RevisionId targetRevisionId;

        await using (var context = fixture.CreateContext())
        {
            var repository = new RfqCaseRepository(context);
            var caseIds = new PostgreSqlCaseIdGenerator(context);
            var seedCase = RfqCase.CreateDraft(
                await caseIds.NextAsync(),
                ClientId.Create("client-seed"),
                SecurityId.Create("security-seed"),
                CategoryId.Create("JGB"),
                UserId.Create("trader-a"),
                100_000_000m,
                new DateOnly(2026, 9, 24),
                new DateOnly(2026, 9, 23),
                null,
                user,
                now);
            seedCase.ConfirmInitial(
                new DateOnly(2026, 9, 21), user, now, seedCase.InitialRevision.Version);
            var targetCase = RfqCase.CreateDraft(
                await caseIds.NextAsync(),
                ClientId.Create("client-target"),
                SecurityId.Create("security-target"),
                CategoryId.Create("JGB"),
                UserId.Create("trader-a"),
                50_000_000m,
                new DateOnly(2026, 9, 24),
                new DateOnly(2026, 9, 23),
                null,
                user,
                now);
            targetCase.ConfirmInitial(
                new DateOnly(2026, 9, 21), user, now, targetCase.InitialRevision.Version);
            seedRevisionId = seedCase.InitialRevision.RevisionId;
            targetRevisionId = targetCase.InitialRevision.RevisionId;
            repository.Add(seedCase);
            repository.Add(targetCase);
            await context.SaveChangesAsync();

            var ensurer = new WorkingQuoteEnsurer(context);
            var seed = await ensurer.EnsureAsync(seedRevisionId, null, user, now);
            await context.SaveChangesAsync();
            var quoteRepository = new WorkingQuoteRepository(context);
            var trackedSeed = Assert.IsType<WorkingQuote>(
                await quoteRepository.GetAsync(seedRevisionId));
            var calculated = new CalculatedQuotePayload(
                CalculationDriver.Price,
                99.5m,
                99.5m,
                0.8m,
                0.81m,
                0.03m,
                0.84m,
                0.83m,
                5m,
                8m);
            trackedSeed.ApplyCalculated(calculated, seed.Version, user, now.AddMinutes(1));
            quoteRepository.Update(trackedSeed);
            await context.SaveChangesAsync();

            var existing = await ensurer.EnsureAsync(seedRevisionId, null, user, now);
            var clone = await ensurer.EnsureAsync(
                targetRevisionId,
                seedRevisionId,
                user,
                now.AddMinutes(2));
            Assert.Equal(2, existing.Version);
            Assert.Equal(1, clone.Version);
            Assert.Equal(calculated, clone.Calculated);
            await context.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var persistedClone = Assert.IsType<WorkingQuote>(
            await new WorkingQuoteRepository(readContext).GetAsync(targetRevisionId));
        Assert.Equal(WorkingQuoteMode.Calculated, persistedClone.Mode);
        Assert.Equal(99.5m, persistedClone.Calculated?.Price);
        Assert.Equal(1, persistedClone.Version);
    }

    [Fact]
    public async Task DirectlyConfirmedCaseRestoresAsOpenLifecycle()
    {
        await RecreateDatabaseAsync();
        var salesUserId = UserId.Create("sales-direct-confirm");
        var confirmedAt = new DateTimeOffset(2026, 9, 21, 1, 0, 0, TimeSpan.Zero);
        long caseId;

        await using (var writeContext = fixture.CreateContext())
        {
            var repository = new RfqCaseRepository(writeContext);
            caseId = (await new PostgreSqlCaseIdGenerator(writeContext).NextAsync()).Value;
            var rfqCase = RfqCase.CreateDraft(
                new CaseId(caseId),
                ClientId.Create("client-direct-confirm"),
                SecurityId.Create("security-direct-confirm"),
                CategoryId.Create("JGB"),
                UserId.Create("trader-direct-confirm"),
                100_000_000m,
                new DateOnly(2026, 9, 24),
                new DateOnly(2026, 9, 23),
                null,
                salesUserId,
                confirmedAt.AddMinutes(-1));
            rfqCase.ConfirmInitial(
                new DateOnly(2026, 9, 21),
                salesUserId,
                confirmedAt,
                rfqCase.InitialRevision.Version);
            repository.Add(rfqCase);
            await new WorkingQuoteEnsurer(writeContext).EnsureAsync(
                rfqCase.InitialRevision.RevisionId,
                null,
                salesUserId,
                confirmedAt);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var restored = Assert.IsType<RfqCase>(
            await new RfqCaseRepository(readContext).GetAsync(new CaseId(caseId)));
        var open = Assert.IsType<OpenRfq>(restored.Lifecycle);
        Assert.Equal(RfqStatus.Active, restored.Status);
        Assert.Equal(QuoteStatus.Requested, open.QuoteStatus);
        Assert.Equal(QuoteRequestReason.Initial, open.QuoteRequestReason);
    }

    [Fact]
    public async Task PartialUniqueIndexRejectsSecondDraftRevisionForCase()
    {
        await RecreateDatabaseAsync();
        var createdAt = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

        await using var context = fixture.CreateContext();
        var repository = new RfqCaseRepository(context);
        var caseId = await new PostgreSqlCaseIdGenerator(context).NextAsync();
        repository.Add(RfqCase.CreateDraft(
            caseId,
            ClientId.Create("client-unique"),
            SecurityId.Create("security-unique"),
            CategoryId.Create("JGB"),
            UserId.Create("trader-unique"),
            null,
            null,
            new DateOnly(2026, 9, 23),
            null,
            UserId.Create("sales-unique"),
            createdAt));
        await context.SaveChangesAsync();

        var secondRevisionId = Guid.NewGuid();
        var exception = await Assert.ThrowsAnyAsync<DbException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO rfq_revisions
                    (revision_id, case_id, status, version, created_at, created_by,
                     standard_settlement_date, sales_and_trading_message)
                VALUES
                    ({secondRevisionId}, {caseId.Value}, 'Draft', 1,
                     {createdAt.AddMinutes(1)}, 'sales-unique',
                     {new DateOnly(2026, 9, 23)}, '')
                """));
        Assert.Contains("ux_rfq_revisions_one_draft_per_case", exception.Message);
    }

    [Fact]
    public async Task ConcurrentOwnershipChangeRejectsSecondTrader()
    {
        await RecreateDatabaseAsync();
        var sales = UserId.Create("sales-concurrency");
        long caseId;

        await using (var seedContext = fixture.CreateContext())
        {
            caseId = (await new PostgreSqlCaseIdGenerator(seedContext).NextAsync()).Value;
            var rfqCase = RfqCase.CreateDraft(
                new CaseId(caseId),
                ClientId.Create("client-concurrency"),
                SecurityId.Create("security-concurrency"),
                CategoryId.Create("JGB"),
                UserId.Create("trader-a"),
                100_000_000m,
                new DateOnly(2026, 9, 24),
                new DateOnly(2026, 9, 23),
                null,
                sales,
                DateTimeOffset.UtcNow);
            rfqCase.ConfirmInitial(
                new DateOnly(2026, 9, 21),
                sales,
                DateTimeOffset.UtcNow,
                rfqCase.InitialRevision.Version);
            new RfqCaseRepository(seedContext).Add(rfqCase);
            await new WorkingQuoteEnsurer(seedContext).EnsureAsync(
                rfqCase.InitialRevision.RevisionId,
                null,
                sales,
                DateTimeOffset.UtcNow);
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var firstRepository = new RfqCaseRepository(firstContext);
        var secondRepository = new RfqCaseRepository(secondContext);
        var first = Assert.IsType<RfqCase>(
            await firstRepository.GetAsync(new CaseId(caseId)));
        var second = Assert.IsType<RfqCase>(
            await secondRepository.GetAsync(new CaseId(caseId)));

        first.PickUp(UserId.Create("trader-a"), first.CurrentVersion);
        second.PickUp(UserId.Create("trader-b"), second.CurrentVersion);
        firstRepository.Update(first);
        secondRepository.Update(second);
        await new EfUnitOfWork(firstContext).SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfUnitOfWork(secondContext).SaveChangesAsync());
        Assert.Contains("changed by another user", exception.Message);
    }

    [Theory]
    [InlineData("375-1", "sec-jgb-375")]
    [InlineData("jgb 0.500 3/20/30 #375", "sec-jgb-375")]
    [InlineData("JP11037", "sec-jgb-375")]
    public async Task SecuritySearchUsesAllSupportedGrammarsAndDeduplicates(
        string query,
        string expectedSecurityId)
    {
        await RecreateDatabaseAsync();
        await using var context = fixture.CreateContext();
        await new DevelopmentDataSeeder(context, TimeProvider.System).SeedAsync();

        var results = await new PostgreSqlSecuritySearch(context).SearchAsync(query);

        Assert.Equal(expectedSecurityId, results[0].SecurityId);
        Assert.Equal(results.Count, results.Select(result => result.SecurityId).Distinct().Count());
    }

    [Fact]
    public async Task MasterDataQueriesReturnClientAndDeterministicDefaults()
    {
        await RecreateDatabaseAsync();
        await using var context = fixture.CreateContext();
        await new DevelopmentDataSeeder(context, TimeProvider.System).SeedAsync();

        var clients = await new PostgreSqlClientSearch(context).SearchAsync("青空");
        var traderId = await new PostgreSqlCategoryRouting(context)
            .GetDefaultAssignedTraderAsync(CategoryId.Create("JGB"));
        var settlementDate = new MockStandardSettlementResolver().Resolve(
            SecurityId.Create("sec-jgb-375"),
            new DateOnly(2026, 9, 18));

        Assert.Equal("client-001", Assert.Single(clients).ClientId);
        Assert.Equal("trader-a", traderId?.Value);
        Assert.Equal(new DateOnly(2026, 9, 22), settlementDate);
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
                Assert.Equal("OTHER", first.CategoryId);
                Assert.Equal("sales-upgrade", first.ContactOwnerId);
                Assert.Equal("trader-a", first.AssignedTraderId);
                Assert.Equal(new DateOnly(2026, 9, 22), first.SettlementDate);
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
