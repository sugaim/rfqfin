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
        await using RfqDbContext context = fixture.CreateContext();
        await context.Database.EnsureDeletedAsync();
        IMigrator migrator = context.GetService<IMigrator>();
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
        string? salesId;
        await using (RfqDbContext context = fixture.CreateContext())
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
                QuoteTransitionKind.Confirmed, new QuoteId(quoteId), UserId.Create("trader-a"), DateTimeOffset.UtcNow));
            await new PostgreSqlUnitOfWork(context, sink).SaveChangesAsync();
        }

        await using RfqDbContext read = fixture.CreateContext();
        var feed = new EfCoreEventFeed(
            read,
            new CurrentUserService(
                new CurrentUser(
                    UserId.Create(salesId),
                    new HashSet<UserRole> { UserRole.Sales },
                    DeskId.Create("jpy-credit"))));
        EventFeedItem item = Assert.Single(await feed.GetAfterAsync(0));
        QuoteConfirmedEvent quoteEvent = Assert.IsType<QuoteConfirmedEvent>(item);
        Assert.Equal(new CaseId(caseId), quoteEvent.CaseId);
        Assert.Equal(new QuoteId(quoteId), quoteEvent.QuoteId);
    }

    [Fact]
    public async Task StateVersion_maps_to_bigint_and_detects_concurrent_updates()
    {
        await RecreateAndSeed();
        CaseId caseId = await AddCase();
        await using RfqDbContext first = fixture.CreateContext();
        await using RfqDbContext second = fixture.CreateContext();
        var firstRepo = new RfqCaseRepository(first);
        var secondRepo = new RfqCaseRepository(second);
        RfqCase a = (await firstRepo.GetAsync(caseId))!;
        RfqCase b = (await secondRepo.GetAsync(caseId))!;
        a = InitialDraftTransitions.Update(
            a,
            UpdatedTerms(a),
            a.AssignedTraderId,
            a.Version);
        b = InitialDraftTransitions.Update(
            b,
            UpdatedTerms(b),
            b.AssignedTraderId,
            b.Version);
        firstRepo.Update(a);
        secondRepo.Update(b);
        await new PostgreSqlUnitOfWork(first).SaveChangesAsync();

        await Assert.ThrowsAsync<StateVersionMismatchException>(
            () => new PostgreSqlUnitOfWork(second).SaveChangesAsync());

        static RevisionTerms UpdatedTerms(RfqCase value) => new(
            value.CurrentRevision.Notional,
            value.CurrentRevision.SettlementDate,
            value.CurrentRevision.StandardSettlementDate,
            "updated");
    }

    [Fact]
    public async Task Discard_after_concurrency_failure_isolates_the_next_item_and_event()
    {
        await RecreateAndSeed();
        CaseId caseId = await AddCase();
        await using RfqDbContext first = fixture.CreateContext();
        await using RfqDbContext second = fixture.CreateContext();
        var firstRepo = new RfqCaseRepository(first);
        var secondRepo = new RfqCaseRepository(second);
        RfqCase winner = (await firstRepo.GetAsync(caseId))!;
        RfqCase stale = (await secondRepo.GetAsync(caseId))!;
        winner = InitialDraftTransitions.Update(
            winner,
            Terms(winner, "winner"),
            winner.AssignedTraderId,
            winner.Version);
        stale = InitialDraftTransitions.Update(
            stale,
            Terms(stale, "stale"),
            stale.AssignedTraderId,
            stale.Version);
        firstRepo.Update(winner);
        secondRepo.Update(stale);
        var sink = new PersistedEventSink();
        sink.Record(new RfqTransition(
            RfqTransitionKind.Cancelled,
            caseId,
            UserId.Create("sales-dev"),
            DateTimeOffset.UtcNow));
        var unitOfWork = new PostgreSqlUnitOfWork(second, sink);
        await new PostgreSqlUnitOfWork(first).SaveChangesAsync();

        await Assert.ThrowsAsync<StateVersionMismatchException>(
            () => unitOfWork.SaveChangesAsync());
        unitOfWork.DiscardChanges();

        RfqCase fresh = (await secondRepo.GetAsync(caseId))!;
        fresh = InitialDraftTransitions.Update(
            fresh,
            Terms(fresh, "next"),
            fresh.AssignedTraderId,
            fresh.Version);
        secondRepo.Update(fresh);
        sink.Record(new RfqTransition(
            RfqTransitionKind.Reopened,
            caseId,
            UserId.Create("sales-dev"),
            DateTimeOffset.UtcNow));
        await unitOfWork.SaveChangesAsync();

        await using RfqDbContext verify = fixture.CreateContext();
        RfqCase persisted = (await new RfqCaseRepository(verify).GetAsync(caseId))!;
        Assert.Equal("next", persisted.CurrentRevision.SalesAndTradingMessage);
        string[] eventTypes = await verify.RfqEvents.Select(value => value.Type).ToArrayAsync();
        Assert.Contains(EventPersistenceTypeCodes.Rfq.Reopened, eventTypes);
        Assert.DoesNotContain(EventPersistenceTypeCodes.Rfq.Cancelled, eventTypes);

        static RevisionTerms Terms(RfqCase value, string message) => new(
            value.CurrentRevision.Notional,
            value.CurrentRevision.SettlementDate,
            value.CurrentRevision.StandardSettlementDate,
            message);
    }

    [Fact]
    public async Task Cursor_lock_prevents_delayed_transaction_from_causing_event_loss()
    {
        await RecreateAndSeed();
        long caseId;
        await using (RfqDbContext lookup = fixture.CreateContext())
        {
            caseId = await lookup.RfqCases.Select(x => x.CaseId).FirstAsync();
        }

        await using RfqDbContext first = fixture.CreateContext();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await first.Database.BeginTransactionAsync();
        EventCursorEntity cursor = await first.EventCursors
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

        await using RfqDbContext second = fixture.CreateContext();
        var sink = new PersistedEventSink();
        sink.Record(new RfqTransition(
            RfqTransitionKind.ContactOwnerChanged,
            new CaseId(caseId),
            UserId.Create("sales-dev"),
            DateTimeOffset.UtcNow,
            From: "sales-dev",
            To: "sales-a"));
        Task secondSave = new PostgreSqlUnitOfWork(second, sink).SaveChangesAsync();
        await Task.Delay(200);
        Assert.False(secondSave.IsCompleted);
        await transaction.CommitAsync();
        await secondSave;

        await using RfqDbContext verify = fixture.CreateContext();
        long[] ids = await verify.Events.OrderBy(x => x.EventId).Select(x => x.EventId).ToArrayAsync();
        Assert.Equal([1L, 2L], ids);
    }

    [Fact]
    public async Task Revision_and_working_quote_uniqueness_constraints_remain()
    {
        await RecreateAndSeed();
        await using RfqDbContext context = fixture.CreateContext();
        List<string> indexes = await context.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname='public'")
            .ToListAsync();
        Assert.Contains("ux_rfq_revisions_one_draft_per_case", indexes);
        Assert.Contains("PK_working_quotes", indexes);
    }

    [Fact]
    public async Task Nullable_sales_id_round_trips_and_trader_query_does_not_repair_missing_quote()
    {
        await RecreateAndSeed();
        var caseId = new CaseId(99_002);
        await using (RfqDbContext write = fixture.CreateContext())
        {
            var trader = UserId.Create("trader-a");
            var draft = RfqCase.CreateDraft(
                caseId,
                RevisionId.New(),
                ClientId.Create("client-001"),
                SecurityId.Create("sec-jgb-375"),
                CategoryId.Create("JGB"),
                trader,
                new RevisionTerms(
                    1_000_000,
                    new DateOnly(2026, 9, 23),
                    new DateOnly(2026, 9, 23),
                    ""),
                trader,
                DateTimeOffset.UtcNow,
                salesId: null);
            RfqCase open = RfqLifecycleTransitions.ConfirmInitial(
                draft,
                draft.CurrentRevision.Terms,
                trader,
                new DateOnly(2026, 9, 21),
                trader,
                DateTimeOffset.UtcNow,
                draft.CurrentRevision.Version);
            new RfqCaseRepository(write).Add(open);
            await new PostgreSqlUnitOfWork(write).SaveChangesAsync();
        }

        await using RfqDbContext read = fixture.CreateContext();
        var repository = new RfqCaseRepository(read);
        RfqCase? restored = await repository.GetAsync(caseId);
        Assert.NotNull(restored);
        Assert.Null(restored.SalesId);
        int before = await read.WorkingQuotes.CountAsync();

        var activeQueries = new EfCoreTraderRfqQueries(read);
        await Assert.ThrowsAsync<DomainInvariantException>(
            () => activeQueries.GetAsync(DeskId.Create("jpy-credit")));

        Assert.Equal(before, await read.WorkingQuotes.CountAsync());
        Assert.False(read.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Past_rfq_date_filters_use_the_current_desk_calendar_day()
    {
        await RecreateAndSeed();
        long[] caseIds;
        await using (RfqDbContext arrange = fixture.CreateContext())
        {
            caseIds = await arrange.RfqCases.OrderBy(item => item.CaseId)
                .Select(item => item.CaseId).Take(4).ToArrayAsync();
            await arrange.RfqCases.ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    item => item.CreatedAt,
                    new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero)));
            DateTimeOffset[] instants =
            [
                new DateTimeOffset(2026, 9, 20, 14, 59, 59, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 21, 14, 59, 59, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 21, 15, 0, 0, TimeSpan.Zero),
            ];
            for (int index = 0; index < caseIds.Length; index++)
            {
                long caseId = caseIds[index];
                DateTimeOffset instant = instants[index];
                await arrange.RfqCases.Where(item => item.CaseId == caseId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.CreatedAt, instant));
            }
        }

        await using RfqDbContext read = fixture.CreateContext();
        var queries = new EfCoreRfqSearchQueries(read, CurrentSales());
        RfqSearchResult result = await queries.SearchAsync(new RfqSearch(
            CreatedFrom: new DateOnly(2026, 9, 21),
            CreatedTo: new DateOnly(2026, 9, 21)));

        Assert.Equal<long>(
            caseIds[1..3],
            result.Items.Select(item => item.CaseId.Value).Order());
    }

    [Fact]
    public async Task Eod_combines_current_open_rfqs_with_close_events_on_the_desk_local_date()
    {
        await RecreateAndSeed();
        await using (RfqDbContext arrange = fixture.CreateContext())
        {
            arrange.Desks.Add(new DeskEntity
            {
                DeskId = "other-desk",
                Name = "Other Desk",
                TimeZoneId = "UTC",
            });
            arrange.MasterUsers.Add(new MasterUserEntity
            {
                UserId = "trader-other",
                Name = "Other Trader",
                DeskId = "other-desk",
                Roles = ["Trader"],
            });
            await arrange.CaseCurrents.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Lifecycle, RfqLifecycleKind.Cancelled)
                .SetProperty(item => item.RfqStatus, RfqStatus.Cancelled));
            CaseCurrentEntity[] rows = await arrange.CaseCurrents.OrderBy(item => item.CaseId)
                .Take(6).ToArrayAsync();
            rows[0].Lifecycle = RfqLifecycleKind.Open;
            rows[0].RfqStatus = RfqStatus.Active;
            rows[1].Lifecycle = RfqLifecycleKind.Closed;
            rows[1].RfqStatus = RfqStatus.Hit;
            rows[2].Lifecycle = RfqLifecycleKind.Closed;
            rows[2].RfqStatus = RfqStatus.Away;
            foreach (CaseCurrentEntity? row in rows)
            {
                row.ContactOwnerId = "sales-dev";
                row.AssignedTraderId = "trader-a";
            }
            rows[3].AssignedTraderId = "trader-other";
            long oldCaseId = rows[0].CaseId;
            await arrange.RfqCases.Where(item => item.CaseId == oldCaseId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    item => item.CreatedAt,
                    new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)));

            long nextEventId = (await arrange.Events.MaxAsync(item => (long?)item.EventId) ?? 0) + 1;
            AddRfqEvent(
                arrange,
                nextEventId++,
                rows[1].CaseId,
                RfqTransitionKind.ClosedHit,
                new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.Zero));
            AddRfqEvent(
                arrange,
                nextEventId++,
                rows[2].CaseId,
                RfqTransitionKind.ClosedAway,
                new DateTimeOffset(2026, 9, 21, 14, 59, 59, TimeSpan.Zero));
            AddRfqEvent(
                arrange,
                nextEventId++,
                rows[3].CaseId,
                RfqTransitionKind.ClosedHit,
                new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero));
            AddRfqEvent(
                arrange,
                nextEventId++,
                rows[4].CaseId,
                RfqTransitionKind.ClosedHit,
                new DateTimeOffset(2026, 9, 20, 14, 59, 59, TimeSpan.Zero));
            AddRfqEvent(
                arrange,
                nextEventId,
                rows[5].CaseId,
                RfqTransitionKind.ClosedAway,
                new DateTimeOffset(2026, 9, 21, 15, 0, 0, TimeSpan.Zero));
            await arrange.SaveChangesAsync();
        }

        await using RfqDbContext read = fixture.CreateContext();
        var queries = new EfCoreEodQueries(read, CurrentSales());
        EodSummaryItem item = Assert.Single(await queries.GetEodAsync(new DateOnly(2026, 9, 21)));

        Assert.Equal(UserId.Create("sales-dev"), item.ContactOwnerId);
        Assert.Equal(1, item.Open);
        Assert.Equal(1, item.Hit);
        Assert.Equal(1, item.Away);
    }

    [Fact]
    public async Task Sales_projection_returns_sales_quote_summary_and_state_transition_time()
    {
        await RecreateAndSeed();
        DateTimeOffset stateSince = new(2026, 9, 22, 1, 23, 0, TimeSpan.Zero);
        long caseId;
        await using (RfqDbContext arrange = fixture.CreateContext())
        {
            CaseCurrentEntity current = await arrange.CaseCurrents
                .Where(item => item.CurrentQuoteId != null || item.ClosedQuoteId != null)
                .OrderBy(item => item.CaseId).FirstAsync();
            caseId = current.CaseId;
            long eventId = (await arrange.Events.MaxAsync(item => (long?)item.EventId) ?? 0) + 1;
            arrange.Events.Add(new EventEntity
            {
                EventId = eventId,
                OccurredAt = stateSince,
                ActorUserId = "sales-dev",
            });
            arrange.RfqEvents.Add(new RfqEventEntity
            {
                EventId = eventId,
                CaseId = caseId,
                Type = EventPersistenceTypeCodes.Rfq.Cancelled,
                PayloadJson = "{}",
            });
            await arrange.SaveChangesAsync();
        }

        await using RfqDbContext read = fixture.CreateContext();
        SalesRfqListItem item = (await new EfCoreSalesRfqQueries(read)
            .GetAsync(UserId.Create("sales-dev"))).Single(value => value.CaseId.Value == caseId);

        Assert.Equal(UserId.Create("sales-dev"), item.SalesId);
        Assert.Equal(stateSince, item.StateSince);
        Assert.NotNull(item.ConfirmedQuote);
        Assert.NotNull(item.ConfirmedQuote.Price);
    }

    [Fact]
    public async Task Sales_projection_maps_manual_quote_missing_values_to_null()
    {
        await RecreateAndSeed();
        long caseId;
        await using (RfqDbContext arrange = fixture.CreateContext())
        {
            CaseCurrentEntity current = await arrange.CaseCurrents
                .Where(item => item.CurrentQuoteId != null)
                .OrderBy(item => item.CaseId).FirstAsync();
            caseId = current.CaseId;
            ConfirmedQuoteEntity quote = await arrange.ConfirmedQuotes.SingleAsync(
                item => item.QuoteId == current.CurrentQuoteId);
            quote.Mode = WorkingQuoteMode.Manual;
            quote.CalculatedPayloadJson = null;
            quote.ManualPayloadJson = QuotePayloadPersistence.Serialize(
                new ManualQuotePayload(99.25m, null));
            await arrange.SaveChangesAsync();
        }

        await using RfqDbContext read = fixture.CreateContext();
        SalesRfqListItem item = (await new EfCoreSalesRfqQueries(read)
            .GetAsync(UserId.Create("sales-dev"))).Single(value => value.CaseId.Value == caseId);

        Assert.Equal(99.25m, item.ConfirmedQuote!.Price);
        Assert.Equal(item.ConfirmedQuote.ConfirmedAt, item.StateSince);
        Assert.Null(item.ConfirmedQuote.BbgYield);
        Assert.Null(item.ConfirmedQuote.FinalSimpleYield);
        Assert.Null(item.ConfirmedQuote.GSpread);
    }

    [Fact]
    public async Task Recent_revisions_excludes_initial_values_and_returns_changed_pairs_newest_first()
    {
        await RecreateAndSeed();
        long caseId;
        var newer = new DateTimeOffset(2026, 9, 22, 2, 0, 0, TimeSpan.Zero);
        await using (RfqDbContext arrange = fixture.CreateContext())
        {
            CaseCurrentEntity current = await arrange.CaseCurrents
                .Where(item => item.CurrentQuoteId != null)
                .OrderBy(item => item.CaseId).FirstAsync();
            caseId = current.CaseId;
            RfqRevisionEntity previous = await arrange.RfqRevisions.SingleAsync(
                item => item.RevisionId == current.CurrentRevisionId);
            previous.Status = RevisionStatus.Superseded;
            var revisionId = Guid.NewGuid();
            arrange.RfqRevisions.Add(new RfqRevisionEntity
            {
                RevisionId = revisionId,
                CaseId = caseId,
                Status = RevisionStatus.Confirmed,
                Version = 2,
                CreatedAt = newer.AddMinutes(-1),
                CreatedBy = "sales-dev",
                ConfirmedAt = newer,
                ConfirmedBy = "sales-dev",
                Notional = previous.Notional + 1_000_000m,
                SettlementDate = previous.SettlementDate,
                StandardSettlementDate = previous.StandardSettlementDate,
                SalesAndTradingMessage = previous.SalesAndTradingMessage,
            });
            arrange.ConfirmedQuotes.Add(new ConfirmedQuoteEntity
            {
                QuoteId = Guid.NewGuid(),
                RevisionId = revisionId,
                SecurityId = "sec-jgb-375",
                SettlementDate = previous.SettlementDate!.Value,
                ConfirmedBy = "trader-a",
                ConfirmedAt = newer.AddMinutes(1),
                Mode = WorkingQuoteMode.Manual,
                ManualPayloadJson = QuotePayloadPersistence.Serialize(
                    new ManualQuotePayload(98.75m, 0.91m)),
                RequestReasonAnswered = QuoteRequestReason.Revised,
            });
            await arrange.SaveChangesAsync();
        }

        await using RfqDbContext read = fixture.CreateContext();
        IReadOnlyList<SalesRecentRevisionItem> items = await new EfCoreSalesRecentRevisionQueries(read)
            .GetAsync(UserId.Create("sales-dev"), 50);

        SalesRecentRevisionItem[] caseItems = [.. items.Where(item => item.CaseId.Value == caseId)];
        Assert.Equal(2, caseItems.Length);
        Assert.Equal(SalesRecentRevisionKind.Quote, caseItems[0].Kind);
        Assert.Equal(SalesRecentRevisionKind.Rfq, caseItems[1].Kind);
        Assert.Contains(caseItems[0].Changes, change => change.Field == SalesRecentRevisionField.Price);
        Assert.Contains(caseItems[1].Changes, change => change.Field == SalesRecentRevisionField.Notional);
        Assert.True(items.Count <= 50);
        Assert.Single(await new EfCoreSalesRecentRevisionQueries(read)
            .GetAsync(UserId.Create("sales-dev"), 1));
        IReadOnlyList<SalesRecentRevisionItem> otherSales = await new EfCoreSalesRecentRevisionQueries(read)
            .GetAsync(UserId.Create("sales-a"), 50);
        Assert.DoesNotContain(otherSales, item => item.CaseId.Value == caseId);
    }

    private static void AddRfqEvent(
        RfqDbContext context,
        long eventId,
        long caseId,
        RfqTransitionKind type,
        DateTimeOffset occurredAt)
    {
        context.Events.Add(new EventEntity
        {
            EventId = eventId,
            OccurredAt = occurredAt,
            ActorUserId = "trader-a",
        });
        context.RfqEvents.Add(new RfqEventEntity
        {
            EventId = eventId,
            CaseId = caseId,
            Type = type switch
            {
                RfqTransitionKind.ClosedHit => EventPersistenceTypeCodes.Rfq.ClosedHit,
                RfqTransitionKind.ClosedAway => EventPersistenceTypeCodes.Rfq.ClosedAway,
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            },
            PayloadJson = "{}",
        });
    }

    private async Task RecreateAndSeed()
    {
        await using RfqDbContext context = fixture.CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        await new DevelopmentDataSeeder(context, TimeProvider.System).SeedAsync();
    }

    private static CurrentUserService CurrentSales() => new(new CurrentUser(
        UserId.Create("sales-dev"),
        new HashSet<UserRole> { UserRole.Sales },
        DeskId.Create("jpy-credit")));

    private async Task<CaseId> AddCase()
    {
        await using RfqDbContext context = fixture.CreateContext();
        var id = new CaseId(99_001);
        var draft = RfqCase.CreateDraft(
            id,
            RevisionId.New(),
            ClientId.Create("client-001"),
            SecurityId.Create("sec-jgb-375"),
            CategoryId.Create("JGB"),
            UserId.Create("trader-a"),
            new RevisionTerms(
                1_000_000,
                new DateOnly(2026, 9, 23),
                new DateOnly(2026, 9, 23),
                ""),
            UserId.Create("sales-dev"),
            DateTimeOffset.UtcNow);
        new RfqCaseRepository(context).Add(draft);
        await new PostgreSqlUnitOfWork(context).SaveChangesAsync();
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
