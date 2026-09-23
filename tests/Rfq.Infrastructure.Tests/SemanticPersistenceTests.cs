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
            DateTimeOffset.UtcNow,
            BusinessDate: new DateOnly(2026, 9, 21)));
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
                new DateOnly(2026, 9, 21),
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
            () => activeQueries.LoadAllAsync(new DateOnly(2026, 9, 21)));

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
        var queries = new EfCoreRfqSearchQueries(
            read, CurrentSales(), new RfqRuntimeOptions());
        RfqSearchResult result = await queries.SearchAsync(new RfqSearch(
            CreatedFrom: new DateOnly(2026, 9, 21),
            CreatedTo: new DateOnly(2026, 9, 21)));

        Assert.Equal<long>(
            caseIds[1..3],
            result.Items.Select(item => item.CaseId.Value).Order());
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
            .LoadAllAsync(new DateOnly(2026, 9, 21)))
            .Single(value => value.CaseId.Value == caseId);

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
            .LoadAllAsync(new DateOnly(2026, 9, 21)))
            .Single(value => value.CaseId.Value == caseId);

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

    [Fact]
    public async Task Post_process_uses_business_facts_for_today_and_current_state_for_unclosed()
    {
        await RecreateAndSeed();
        var today = new DateOnly(2026, 9, 21);
        DateOnly oldBusinessDate = today.AddDays(-1);
        long[] caseIds;
        await using (RfqDbContext arrange = fixture.CreateContext())
        {
            RfqCaseEntity[] cases = await arrange.RfqCases
                .Include(item => item.Current)
                .Include(item => item.SalesMemo)
                .OrderBy(item => item.CaseId)
                .Take(8)
                .ToArrayAsync();
            caseIds = [.. cases.Select(item => item.CaseId)];
            foreach (RfqCaseEntity rfqCase in cases.Where(item => item.CreatedBusinessDate != null))
            {
                rfqCase.CreatedBusinessDate = oldBusinessDate;
            }

            // Created today. The remaining active cases model quote-only and memo-only
            // activity, neither of which is a Today business fact.
            cases[1].CreatedBusinessDate = today;
            ConfirmedQuoteEntity quoteOnly = await arrange.ConfirmedQuotes.SingleAsync(
                item => item.QuoteId == cases[2].Current.CurrentQuoteId);
            quoteOnly.ConfirmedAt = new DateTimeOffset(
                2026, 9, 21, 3, 0, 0, TimeSpan.Zero);
            cases[5].SalesMemo.Value = "memo changed without a business event";

            cases[1].SalesId = "sales-dev";
            cases[1].Current.ContactOwnerId = "sales-a";
            cases[1].Current.AssignedTraderId = "trader-a";
            cases[2].SalesId = null;
            cases[2].Current.ContactOwnerId = "sales-dev";
            cases[2].Current.AssignedTraderId = "trader-a";
            cases[3].SalesId = null;
            cases[3].Current.ContactOwnerId = "sales-a";
            cases[3].Current.AssignedTraderId = "sales-dev";
            cases[5].SalesId = "sales-a";
            cases[5].Current.ContactOwnerId = "sales-a";
            cases[5].Current.AssignedTraderId = "trader-a";

            AddPostProcessEvent(
                arrange,
                1,
                cases[6].CaseId,
                EventPersistenceTypeCodes.Rfq.ClosedHit,
                today,
                PersistenceJsonSerializer.Serialize(new ClosePayload
                {
                    QuoteId = cases[6].Current.ClosedQuoteId!.Value,
                    BusinessDate = today,
                }));
            AddPostProcessEvent(
                arrange,
                2,
                cases[7].CaseId,
                EventPersistenceTypeCodes.Rfq.OutcomeCorrected,
                today,
                PersistenceJsonSerializer.Serialize(new OutcomeCorrectedPayload
                {
                    QuoteId = cases[7].Current.ClosedQuoteId!.Value,
                    From = "Hit",
                    To = "Away",
                    Reason = "same-day correction",
                    BusinessDate = today,
                }));
            await arrange.SaveChangesAsync();
        }

        var permitted = caseIds.Select(value => new CaseId(value)).ToHashSet();
        CurrentUser currentUser = CurrentSales().User;
        await using RfqDbContext read = fixture.CreateContext();
        var queries = new EfCorePostProcessQueries(read);

        IReadOnlyList<PostProcessWorklistItem> todayItems = await queries.GetAsync(
            PostProcessPreset.Today,
            PostProcessScope.AllPermitted,
            today,
            currentUser,
            permitted);
        Assert.Equal<long>(
            [caseIds[1], caseIds[6], caseIds[7]],
            todayItems.Select(item => item.CaseId.Value).Order());
        Assert.DoesNotContain(todayItems, item => item.CaseId.Value == caseIds[0]);
        Assert.DoesNotContain(todayItems, item => item.CaseId.Value == caseIds[2]);
        Assert.DoesNotContain(todayItems, item => item.CaseId.Value == caseIds[5]);
        Assert.Equal(
            "same-day correction",
            todayItems.Single(item => item.CaseId.Value == caseIds[7]).LastCorrectionReason);

        IReadOnlyList<PostProcessWorklistItem> unclosed = await queries.GetAsync(
            PostProcessPreset.Unclosed,
            PostProcessScope.AllPermitted,
            today,
            currentUser,
            permitted);
        Assert.Equal<long>(
            [caseIds[1], caseIds[2], caseIds[3], caseIds[5]],
            unclosed.Select(item => item.CaseId.Value).Order());

        IReadOnlyList<PostProcessWorklistItem> mine = await queries.GetAsync(
            PostProcessPreset.Unclosed,
            PostProcessScope.Mine,
            today,
            currentUser,
            permitted);
        Assert.Equal<long>(
            [caseIds[1], caseIds[2], caseIds[3]],
            mine.Select(item => item.CaseId.Value).Order());
    }

    [Fact]
    public async Task Closed_business_dates_round_trip_with_the_case_and_event()
    {
        await RecreateAndSeed();
        await using RfqDbContext context = fixture.CreateContext();
        RfqCaseEntity entity = await context.RfqCases
            .Include(item => item.Current)
            .FirstAsync(item => item.Current.RfqStatus == RfqStatus.Hit);
        var repository = new RfqCaseRepository(context);

        RfqCase restored = (await repository.GetAsync(new CaseId(entity.CaseId)))!;

        Assert.Equal(new DateOnly(2026, 9, 21), restored.CreatedBusinessDate);
        Assert.Equal(new DateOnly(2026, 9, 21), restored.ClosedBusinessDate);
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

    private static void AddPostProcessEvent(
        RfqDbContext context,
        long eventId,
        long caseId,
        string type,
        DateOnly businessDate,
        string payloadJson)
    {
        context.Events.Add(new EventEntity
        {
            EventId = eventId,
            OccurredAt = new DateTimeOffset(
                2026, 9, 21, checked((int)eventId), 0, 0, TimeSpan.Zero),
            ActorUserId = "sales-dev",
        });
        context.RfqEvents.Add(new RfqEventEntity
        {
            EventId = eventId,
            CaseId = caseId,
            Type = type,
            BusinessDate = businessDate,
            PayloadJson = payloadJson,
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

    [Fact]
    public async Task Snapshot_membership_uses_persisted_case_or_revision_business_date()
    {
        await RecreateAndSeed();
        var today = new DateOnly(2026, 9, 21);
        DateOnly oldDate = today.AddDays(-1);
        long draftTodayId;
        long confirmedTodayId;
        long confirmedAmendmentId;
        long discardedAmendmentId;
        long timestampOnlyId;
        await using (RfqDbContext arrange = fixture.CreateContext())
        {
            await arrange.RfqCases.ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    item => item.CreatedBusinessDate,
                    item => item.CreatedBusinessDate == null ? null : oldDate));
            await arrange.RfqRevisions.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DraftCreatedBusinessDate, oldDate));

            RfqCaseEntity draftToday = await arrange.RfqCases
                .Include(item => item.Current).ThenInclude(item => item.CurrentRevision)
                .FirstAsync(item => item.Current.Lifecycle == RfqLifecycleKind.Draft);
            List<RfqCaseEntity> openCases = await arrange.RfqCases
                .Include(item => item.Current).ThenInclude(item => item.CurrentRevision)
                .Where(item => item.Current.Lifecycle == RfqLifecycleKind.Open)
                .OrderBy(item => item.CaseId)
                .Take(4)
                .ToListAsync();
            draftTodayId = draftToday.CaseId;
            confirmedTodayId = openCases[0].CaseId;
            confirmedAmendmentId = openCases[1].CaseId;
            discardedAmendmentId = openCases[2].CaseId;
            timestampOnlyId = openCases[3].CaseId;
            draftToday.Current.CurrentRevision.DraftCreatedBusinessDate = today;
            openCases[0].CreatedBusinessDate = today;
            arrange.RfqRevisions.Add(HistoricalRevision(openCases[1], today, RevisionStatus.Superseded));
            arrange.RfqRevisions.Add(HistoricalRevision(openCases[2], today, RevisionStatus.Discarded));
            openCases[3].CreatedAt = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
            await arrange.SaveChangesAsync();
        }

        await using RfqDbContext read = fixture.CreateContext();
        BusinessDateRfqSnapshot snapshot = await new BusinessDateRfqSnapshotLoader(read)
            .LoadAsync(today, 7);
        long[] salesIds = [.. snapshot.Sales.Select(item => item.CaseId.Value)];
        long[] traderIds = [.. snapshot.Trader.Select(item => item.CaseId.Value)];
        Assert.Contains(draftTodayId, salesIds);
        Assert.Contains(confirmedTodayId, salesIds);
        Assert.Contains(confirmedAmendmentId, salesIds);
        Assert.Contains(discardedAmendmentId, salesIds);
        Assert.DoesNotContain(timestampOnlyId, salesIds);
        Assert.DoesNotContain(draftTodayId, traderIds);
        Assert.Contains(confirmedTodayId, traderIds);
        Assert.Contains(confirmedAmendmentId, traderIds);
        Assert.Contains(discardedAmendmentId, traderIds);
        Assert.Equal(7, snapshot.Generation);
    }

    [Fact]
    public async Task Unit_of_work_invalidates_only_after_successful_no_event_commit()
    {
        await RecreateAndSeed();
        await using RfqDbContext context = fixture.CreateContext();
        var signal = new RecordingCommittedChangeSignal();
        var draft = RfqCase.CreateDraft(
            new CaseId(199_001),
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
            new DateOnly(2026, 9, 21),
            UserId.Create("sales-dev"),
            DateTimeOffset.UtcNow);
        new RfqCaseRepository(context).Add(draft);

        await new PostgreSqlUnitOfWork(
            context, new PersistedEventSink(), signal).SaveChangesAsync();

        Assert.Equal(1, signal.Count);
    }

    [Fact]
    public async Task Failed_commit_does_not_invalidate_snapshot()
    {
        await RecreateAndSeed();
        await using RfqDbContext context = fixture.CreateContext();
        var signal = new RecordingCommittedChangeSignal();
        context.SeedMarkers.Add(new SeedMarker(
            DevelopmentDataSeeder.BusinessDateSeedKey,
            DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => new PostgreSqlUnitOfWork(
            context, new PersistedEventSink(), signal).SaveChangesAsync());

        Assert.Equal(0, signal.Count);
    }

    [Fact]
    public async Task Semantic_event_transaction_invalidates_after_commit()
    {
        await RecreateAndSeed();
        await using RfqDbContext context = fixture.CreateContext();
        long caseId = await context.RfqCases.Select(item => item.CaseId).FirstAsync();
        var signal = new RecordingCommittedChangeSignal();
        var sink = new PersistedEventSink();
        sink.Record(new RfqTransition(
            RfqTransitionKind.Reopened,
            new CaseId(caseId),
            UserId.Create("sales-dev"),
            DateTimeOffset.UtcNow));

        await new PostgreSqlUnitOfWork(context, sink, signal).SaveChangesAsync();

        Assert.Equal(1, signal.Count);
    }

    private static RfqRevisionEntity HistoricalRevision(
        RfqCaseEntity rfqCase,
        DateOnly businessDate,
        RevisionStatus status) => new()
        {
            RevisionId = Guid.NewGuid(),
            CaseId = rfqCase.CaseId,
            Status = status,
            Version = 2,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            DraftCreatedBusinessDate = businessDate,
            CreatedBy = rfqCase.CreatedBy,
            SettlementDate = rfqCase.Current.CurrentRevision.SettlementDate,
            StandardSettlementDate = rfqCase.Current.CurrentRevision.StandardSettlementDate,
            Notional = rfqCase.Current.CurrentRevision.Notional,
            SalesAndTradingMessage = "historical amendment",
        };

    private sealed class RecordingCommittedChangeSignal : ICommittedRfqChangeSignal
    {
        public int Count { get; private set; }
        public void SignalCommittedChange() => Count++;
    }

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
            new DateOnly(2026, 9, 21),
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
