using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rfq.Application;
using Rfq.Domain;
using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class SemanticPersistenceTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private const string PreviousMigration =
        "20260921055755_AddAmendmentsLifecycleEventsAndOperations";

    [Fact]
    public async Task Migration_upgrades_existing_schema_and_removes_quote_event_case_id()
    {
        await using var context = fixture.CreateContext();
        await context.Database.EnsureDeletedAsync();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        Assert.True(await ColumnExists(context, "quote_events", "case_id"));

        await context.Database.MigrateAsync();

        Assert.False(await ColumnExists(context, "quote_events", "case_id"));
        Assert.True(await ColumnExists(context, "quote_events", "quote_id"));
    }

    [Fact]
    public async Task Quote_event_feed_derives_case_through_quote_and_revision()
    {
        await RecreateAndSeed();
        Guid quoteId;
        long caseId;
        string salesId;
        await using (var context = fixture.CreateContext())
        {
            var row = await (
                from quote in context.ConfirmedQuotes
                join revision in context.RfqRevisions on quote.RevisionId equals revision.RevisionId
                join rfq in context.RfqCases on revision.CaseId equals rfq.CaseId
                select new { quote.QuoteId, revision.CaseId, rfq.SalesId }).FirstAsync();
            quoteId = row.QuoteId;
            caseId = row.CaseId;
            salesId = row.SalesId;
            var sink = new PersistedEventSink();
            sink.Record(new QuoteTransition(
                QuoteTransitionKind.Confirmed, quoteId, "trader-a", DateTimeOffset.UtcNow));
            await new EfUnitOfWork(context, sink).SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        var feed = new PostgreSqlEventFeed(read, new CurrentUserService(
            new CurrentUser(UserId.Create(salesId), new HashSet<UserRole> { UserRole.Sales }, "desk-jp")));
        var item = Assert.Single(await feed.GetAfterAsync(0));
        Assert.Equal(caseId, item.CaseId);
        Assert.Equal("Quote", item.Kind);
    }

    [Fact]
    public async Task StateVersion_maps_to_bigint_and_detects_concurrent_updates()
    {
        await RecreateAndSeed();
        var caseId = await AddCase();
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstRepo = new RfqCaseRepository(first);
        var secondRepo = new RfqCaseRepository(second);
        var a = (await firstRepo.GetAsync(caseId))!;
        var b = (await secondRepo.GetAsync(caseId))!;
        a = RfqResponsibilityTransitions.ChangeContactOwner(
            a, UserId.Create("sales-a"), a.Version);
        b = RfqResponsibilityTransitions.ChangeContactOwner(
            b, UserId.Create("sales-a"), b.Version);
        firstRepo.Update(a);
        secondRepo.Update(b);
        await new EfUnitOfWork(first).SaveChangesAsync();

        await Assert.ThrowsAsync<StateVersionMismatchException>(
            () => new EfUnitOfWork(second).SaveChangesAsync());
    }

    [Fact]
    public async Task Cursor_lock_prevents_delayed_transaction_from_causing_event_loss()
    {
        await RecreateAndSeed();
        long caseId;
        await using (var lookup = fixture.CreateContext())
            caseId = await lookup.RfqCases.Select(x => x.CaseId).FirstAsync();
        await using var first = fixture.CreateContext();
        await using var transaction = await first.Database.BeginTransactionAsync();
        var cursor = await first.EventCursors
            .FromSqlRaw("SELECT * FROM event_cursors WHERE cursor_key = 'global' FOR UPDATE")
            .SingleAsync();
        cursor.LastEventId++;
        first.Events.Add(new EventEntity
        {
            EventId = cursor.LastEventId,
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserId = "sales-dev",
        });
        first.RfqEvents.Add(new RfqEventEntity
        {
            EventId = cursor.LastEventId,
            CaseId = caseId,
            Type = "First",
            PayloadJson = "{}",
        });
        await first.SaveChangesAsync();

        await using var second = fixture.CreateContext();
        var sink = new PersistedEventSink();
        sink.Record(new RfqTransition(
            RfqTransitionKind.ContactOwnerChanged, caseId, "sales-dev",
            DateTimeOffset.UtcNow));
        var secondSave = new EfUnitOfWork(second, sink).SaveChangesAsync();
        await Task.Delay(200);
        Assert.False(secondSave.IsCompleted);
        await transaction.CommitAsync();
        await secondSave;

        await using var verify = fixture.CreateContext();
        var ids = await verify.Events.OrderBy(x => x.EventId).Select(x => x.EventId).ToArrayAsync();
        Assert.Equal([1L, 2L], ids);
    }

    [Fact]
    public async Task Revision_and_working_quote_uniqueness_constraints_remain()
    {
        await RecreateAndSeed();
        await using var context = fixture.CreateContext();
        var indexes = await context.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname='public'")
            .ToListAsync();
        Assert.Contains("ux_rfq_revisions_one_draft_per_case", indexes);
        Assert.Contains("PK_working_quotes", indexes);
    }

    private async Task RecreateAndSeed()
    {
        await using var context = fixture.CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        await new DevelopmentDataSeeder(context, TimeProvider.System).SeedAsync();
    }

    private async Task<CaseId> AddCase()
    {
        await using var context = fixture.CreateContext();
        var id = new CaseId(99_001);
        var draft = RfqCase.CreateDraft(
            id, RevisionId.New(), ClientId.Create("client-001"),
            SecurityId.Create("sec-jgb-375"), CategoryId.Create("JGB"),
            UserId.Create("trader-a"),
            new RevisionTerms(1_000_000, new DateOnly(2026, 9, 23),
                new DateOnly(2026, 9, 23), ""),
            UserId.Create("sales-dev"), DateTimeOffset.UtcNow);
        new RfqCaseRepository(context).Add(draft);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return id;
    }

    private static async Task<bool> ColumnExists(
        RfqDbContext context, string table, string column) =>
        await context.Database.SqlQueryRaw<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns " +
            $"WHERE table_schema='public' AND table_name='{table}' AND column_name='{column}') AS \"Value\"")
            .SingleAsync();

    private sealed record CurrentUserService(CurrentUser User) : ICurrentUser;
}
