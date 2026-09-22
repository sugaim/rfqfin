using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rfq.Infrastructure;
using Xunit;

namespace Rfq.Api.Tests;

public sealed class RfqEndpointsTests(RfqApiFixture fixture)
    : IClassFixture<RfqApiFixture>
{
    [Fact]
    public async Task InitialDraftCanBeSavedEditedConfirmedOrDiscarded()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage saveResponse = await client.PostAsJsonAsync(
            "/api/rfqs/drafts",
            DraftRequest("client-001", "sec-jgb-375"));
        InitialRfqBody saved = await AssertCreatedAsync(saveResponse);
        Assert.Equal("Draft", saved.RfqStatus);
        Assert.Equal("Draft", saved.RevisionStatus);
        Assert.Null(saved.Notional);
        Assert.Equal(new DateOnly(2026, 9, 24), saved.SettlementDate);
        Assert.Null(saved.QuoteStatus);
        Assert.Equal(1, saved.Version);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/rfqs/{saved.CaseId}/draft",
            new
            {
                Notional = 100_000_000m,
                SettlementDate = "2026-09-24",
                StandardSettlementDate = "2026-09-23",
                SalesAndTradingMessage = "Please quote",
                AssignedTraderId = "trader-a",
                ExpectedVersion = saved.Version,
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        InitialRfqBody? updated = await updateResponse.Content.ReadFromJsonAsync<InitialRfqBody>();
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Version);
        Assert.Equal(100_000_000m, updated.Notional);

        HttpResponseMessage confirmResponse = await client.PostAsJsonAsync(
            $"/api/rfqs/{saved.CaseId}/draft/confirm",
            new
            {
                updated.Notional,
                SettlementDate = "2026-09-24",
                StandardSettlementDate = "2026-09-23",
                updated.SalesAndTradingMessage,
                updated.AssignedTraderId,
                ExpectedVersion = updated.Version,
            });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        InitialRfqBody? confirmed = await confirmResponse.Content.ReadFromJsonAsync<InitialRfqBody>();
        Assert.NotNull(confirmed);
        Assert.Equal("Active", confirmed.RfqStatus);
        Assert.Equal("Confirmed", confirmed.RevisionStatus);
        Assert.Equal("Requested", confirmed.QuoteStatus);
        Assert.Equal("Initial", confirmed.QuoteRequestReason);
        Assert.Equal(3, confirmed.Version);

        HttpResponseMessage directResponse = await client.PostAsJsonAsync(
            "/api/rfqs/drafts/confirm",
            new
            {
                ClientId = "client-002",
                SecurityId = "sec-toyota-1",
                Notional = 50_000_000m,
                SettlementDate = "2026-09-24",
                StandardSettlementDate = "2026-09-23",
                SalesAndTradingMessage = "Direct confirmation",
                AssignedTraderId = "trader-b",
            });
        InitialRfqBody directlyConfirmed = await AssertCreatedAsync(directResponse);
        Assert.Equal(saved.CaseId + 1, directlyConfirmed.CaseId);
        Assert.Equal("Active", directlyConfirmed.RfqStatus);
        Assert.Equal("Confirmed", directlyConfirmed.RevisionStatus);
        Assert.Equal("Requested", directlyConfirmed.QuoteStatus);
        Assert.Equal("Initial", directlyConfirmed.QuoteRequestReason);

        HttpResponseMessage discardSaveResponse = await client.PostAsJsonAsync(
            "/api/rfqs/drafts",
            DraftRequest("client-003", "sec-jgb-375"));
        InitialRfqBody toDiscard = await AssertCreatedAsync(discardSaveResponse);
        HttpResponseMessage discardResponse = await client.PostAsJsonAsync(
            $"/api/rfqs/{toDiscard.CaseId}/draft/discard",
            new { ExpectedVersion = toDiscard.Version });
        Assert.Equal(HttpStatusCode.NoContent, discardResponse.StatusCode);

        List<SalesRfqBody>? rows = await client.GetFromJsonAsync<List<SalesRfqBody>>(
            "/api/sales-rfqs");
        List<SalesRfqBody> list = Assert.IsType<List<SalesRfqBody>>(rows);
        SalesRfqBody confirmedRow = Assert.Single(list, item => item.CaseId == saved.CaseId);
        Assert.Equal("青空銀行", confirmedRow.ClientName);
        Assert.Equal("利付国債 第375回", confirmedRow.SecurityJapaneseName);
        Assert.Equal("JGB 0.5 03/20/2030 #375", confirmedRow.SecurityBbgDisplay);
        Assert.Equal("Active", confirmedRow.RfqStatus);
        Assert.Equal("Confirmed", confirmedRow.RevisionStatus);
        Assert.Equal("Requested", confirmedRow.QuoteStatus);
        Assert.Equal("Initial", confirmedRow.QuoteRequestReason);
        Assert.DoesNotContain(list, item => item.CaseId == toDiscard.CaseId);
    }

    [Fact]
    public async Task ConfirmRejectsIncompleteDraft()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/rfqs/drafts/confirm",
            new { ClientId = "client-004", SecurityId = "sec-jgb-375" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TraderOwnershipWorkflowSupportsRoutingPermissionsAndConcurrency()
    {
        using HttpClient sales = CreateClient("sales-dev");
        HttpResponseMessage createResponse = await sales.PostAsJsonAsync(
            "/api/rfqs/drafts/confirm",
            new
            {
                ClientId = "client-001",
                SecurityId = "sec-jgb-375",
                Notional = 100_000_000m,
                SettlementDate = "2026-09-24",
                StandardSettlementDate = "2026-09-23",
                SalesAndTradingMessage = "",
                AssignedTraderId = "trader-a",
            });
        InitialRfqBody created = await AssertCreatedAsync(createResponse);

        using HttpClient traderA = CreateClient("trader-a");
        List<TraderRfqBody>? rows = await traderA.GetFromJsonAsync<List<TraderRfqBody>>(
            "/api/trader-rfqs");
        TraderRfqBody row = Assert.Single(rows!, item => item.CaseId == created.CaseId);
        Assert.False(row.Owned);
        Assert.Equal("trader-a", row.AssignedTraderId);
        Assert.Equal(string.Empty, row.SalesAndTradingMessage);
        Assert.NotEqual(default, row.StateSince);

        HttpResponseMessage pickUp = await traderA.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/ownership/pick-up",
            new { row.ExpectedVersion, Confirmed = false });
        OwnershipBody pickedUp = await AssertOwnershipOkAsync(pickUp);
        Assert.True(pickedUp.Owned);
        Assert.Equal("trader-a", pickedUp.AssignedTraderId);

        HttpResponseMessage calculateResponse = await traderA.PutAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/working-quote/calculate",
            new
            {
                Driver = "Price",
                Value = 99.5m,
                SimpleYieldSlide = 0.03m,
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = row.WorkingQuoteVersion,
            });
        WorkingQuoteBody calculated = await AssertWorkingQuoteOkAsync(calculateResponse);
        Assert.Equal("Calculated", calculated.Mode);
        Assert.Equal(99.5m, calculated.Calculated?.Price);
        Assert.Equal(
            calculated.Calculated?.BaseSimpleYield + 0.03m,
            calculated.Calculated?.FinalSimpleYield);
        Assert.NotNull(calculated.Calculated?.Ysc);
        Assert.NotNull(calculated.Calculated?.ISpread);
        Assert.NotNull(calculated.Calculated?.ZSpread);

        HttpResponseMessage failedCalculation = await traderA.PutAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/working-quote/calculate",
            new
            {
                Driver = "Price",
                Value = -999m,
                SimpleYieldSlide = 0m,
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = calculated.Version,
            });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failedCalculation.StatusCode);
        using (var failureDocument = JsonDocument.Parse(
            await failedCalculation.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                "CalculationFailure",
                failureDocument.RootElement.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(failureDocument.RootElement
                .GetProperty("calculationErrorCode").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(failureDocument.RootElement
                .GetProperty("failureLogId").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(failureDocument.RootElement
                .GetProperty("traceId").GetString()));
        }
        await using (AsyncServiceScope failureScope = fixture.Factory.Services.CreateAsyncScope())
        {
            RfqDbContext dbContext = failureScope.ServiceProvider.GetRequiredService<RfqDbContext>();
            int failureCount = await dbContext.Database.SqlQuery<int>(
                $"""
                SELECT COUNT(*)::int AS "Value"
                FROM calculation_failure_logs
                WHERE case_id = {created.CaseId}
                """).SingleAsync();
            Assert.Equal(1, failureCount);
        }

        HttpResponseMessage manualModeResponse = await traderA.PutAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/working-quote/mode",
            new
            {
                Mode = "Manual",
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = calculated.Version,
            });
        WorkingQuoteBody manualMode = await AssertWorkingQuoteOkAsync(manualModeResponse);
        Assert.Equal("Manual", manualMode.Mode);
        Assert.Null(manualMode.Manual?.Price);
        Assert.Equal(calculated.Calculated, manualMode.Calculated);

        HttpResponseMessage manualUpdateResponse = await traderA.PutAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/working-quote/manual",
            new
            {
                Price = 98.75m,
                FinalSimpleYield = 1.25m,
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = manualMode.Version,
            });
        WorkingQuoteBody manualUpdate = await AssertWorkingQuoteOkAsync(manualUpdateResponse);
        Assert.Equal(98.75m, manualUpdate.Manual?.Price);
        Assert.Equal(1.25m, manualUpdate.Manual?.FinalSimpleYield);

        HttpResponseMessage calculatedModeResponse = await traderA.PutAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/working-quote/mode",
            new
            {
                Mode = "Calculated",
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = manualUpdate.Version,
            });
        WorkingQuoteBody restoredCalculated = await AssertWorkingQuoteOkAsync(calculatedModeResponse);
        Assert.Equal(calculated.Calculated, restoredCalculated.Calculated);

        HttpResponseMessage confirmQuoteResponse = await traderA.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/quote/confirm",
            new
            {
                Expiry = new { Type = "After", Minutes = 5 },
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = restoredCalculated.Version,
            });
        ConfirmQuoteBody confirmedQuote = await AssertQuoteConfirmedAsync(confirmQuoteResponse);
        Assert.Equal("Quoted", confirmedQuote.QuoteStatus);
        Assert.Equal("Active", confirmedQuote.RfqStatus);
        Assert.Equal(restoredCalculated.Calculated, confirmedQuote.Calculated);
        Assert.Equal(confirmedQuote.ExpiresAt, confirmedQuote.ConfirmedAt?.AddMinutes(5));

        HttpResponseMessage secondConfirm = await traderA.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/quote/confirm",
            new
            {
                Expiry = new { Type = "After", Minutes = 5 },
                ExpectedCurrentVersion = confirmedQuote.CurrentVersion,
                ExpectedWorkingQuoteVersion = restoredCalculated.Version,
            });
        Assert.Equal(HttpStatusCode.Conflict, secondConfirm.StatusCode);

        HttpResponseMessage editQuoted = await traderA.PutAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/working-quote/calculate",
            new
            {
                Driver = "Price",
                Value = 98m,
                SimpleYieldSlide = 0m,
                ExpectedCurrentVersion = confirmedQuote.CurrentVersion,
                ExpectedWorkingQuoteVersion = restoredCalculated.Version,
            });
        Assert.Equal(HttpStatusCode.Conflict, editQuoted.StatusCode);

        HttpResponseMessage forbiddenPresent = await traderA.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/present",
            new { ExpectedCurrentVersion = confirmedQuote.CurrentVersion });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenPresent.StatusCode);

        HttpResponseMessage presentResponse = await sales.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/present",
            new { ExpectedCurrentVersion = confirmedQuote.CurrentVersion });
        PresentationBody presented = await AssertPresentationOkAsync(presentResponse);
        Assert.Equal("Presented", presented.RfqStatus);
        Assert.Equal("Quoted", presented.QuoteStatus);

        HttpResponseMessage unpresentResponse = await sales.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/unpresent",
            new { ExpectedCurrentVersion = presented.CurrentVersion });
        PresentationBody unpresented = await AssertPresentationOkAsync(unpresentResponse);
        Assert.Equal("Active", unpresented.RfqStatus);
        Assert.Equal(confirmedQuote.QuoteId, unpresented.QuoteId);

        HttpResponseMessage release = await traderA.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/ownership/release",
            new { ExpectedVersion = unpresented.CurrentVersion });
        OwnershipBody released = await AssertOwnershipOkAsync(release);
        Assert.False(released.Owned);

        HttpResponseMessage assign = await traderA.PutAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/assigned-trader",
            new { AssignedTraderId = "trader-b", ExpectedVersion = released.CurrentVersion });
        OwnershipBody assigned = await AssertOwnershipOkAsync(assign);
        Assert.Equal("trader-b", assigned.AssignedTraderId);
        Assert.False(assigned.Owned);

        HttpResponseMessage missingConfirmation = await traderA.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/ownership/pick-up",
            new { ExpectedVersion = assigned.CurrentVersion, Confirmed = false });
        Assert.Equal(HttpStatusCode.BadRequest, missingConfirmation.StatusCode);

        using HttpClient traderB = CreateClient("trader-b");
        HttpResponseMessage ownerBResponse = await traderB.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/ownership/pick-up",
            new { ExpectedVersion = assigned.CurrentVersion, Confirmed = false });
        OwnershipBody ownerB = await AssertOwnershipOkAsync(ownerBResponse);
        Assert.True(ownerB.Owned);

        HttpResponseMessage forbiddenRelease = await traderA.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/ownership/release",
            new { ExpectedVersion = ownerB.CurrentVersion });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRelease.StatusCode);

        HttpResponseMessage takeOverResponse = await traderA.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/ownership/take-over",
            new { ExpectedVersion = ownerB.CurrentVersion, Confirmed = true });
        OwnershipBody takenOver = await AssertOwnershipOkAsync(takeOverResponse);
        Assert.True(takenOver.Owned);
        Assert.Equal("trader-a", takenOver.AssignedTraderId);

        HttpResponseMessage staleTakeOver = await traderB.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/ownership/take-over",
            new { ExpectedVersion = ownerB.CurrentVersion, Confirmed = true });
        Assert.Equal(HttpStatusCode.Conflict, staleTakeOver.StatusCode);

        HttpResponseMessage traderCreate = await traderA.PostAsJsonAsync(
            "/api/rfqs/drafts",
            DraftRequest("client-001", "sec-jgb-375"));
        InitialRfqBody traderCreated = await AssertCreatedAsync(traderCreate);
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        RfqDbContext traderDbContext = scope.ServiceProvider.GetRequiredService<RfqDbContext>();
        int nullSalesCount = await traderDbContext.Database.SqlQuery<int>(
            $"""
            SELECT COUNT(*)::int AS "Value"
            FROM rfq_cases
            WHERE case_id = {traderCreated.CaseId} AND sales_id IS NULL
            """).SingleAsync();
        Assert.Equal(1, nullSalesCount);
    }

    [Fact]
    public async Task RevisionChangesRequireContactOwnerThroughCentralAuthorization()
    {
        using HttpClient owner = CreateClient("sales-dev");
        HttpResponseMessage createResponse = await owner.PostAsJsonAsync(
            "/api/rfqs/drafts",
            DraftRequest("client-004", "sec-other-1", "trader-b"));
        InitialRfqBody draft = await AssertCreatedAsync(createResponse);

        using HttpClient otherSales = CreateClient("sales-a");
        HttpResponseMessage response = await otherSales.PutAsJsonAsync(
            $"/api/rfqs/{draft.CaseId}/draft",
            new
            {
                Notional = 1_000_000m,
                SettlementDate = "2026-09-24",
                StandardSettlementDate = "2026-09-23",
                SalesAndTradingMessage = "",
                AssignedTraderId = "trader-b",
                ExpectedVersion = draft.Version,
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HitAwayContactOwnerAndRoleMemosCompleteTheHappyPath()
    {
        using HttpClient sales = CreateClient("sales-dev");
        using HttpClient trader = CreateClient("trader-a");
        ConfirmQuoteBody hitCase = await CreateQuotedCaseAsync(sales, trader, "client-001");
        ConfirmQuoteBody awayCase = await CreateQuotedCaseAsync(sales, trader, "client-002");

        HttpResponseMessage hitResponse = await sales.PostAsJsonAsync(
            $"/api/rfqs/{hitCase.CaseId}/close/hit",
            new { ExpectedCurrentVersion = hitCase.CurrentVersion });
        CloseRfqBody hit = await AssertCaseClosedAsync(hitResponse);
        Assert.Equal("Hit", hit.RfqStatus);
        Assert.Equal(hitCase.QuoteId, hit.ClosedQuoteId);
        Assert.False(hit.Owned);

        HttpResponseMessage awayResponse = await sales.PostAsJsonAsync(
            $"/api/rfqs/{awayCase.CaseId}/close/away",
            new { ExpectedCurrentVersion = awayCase.CurrentVersion });
        CloseRfqBody away = await AssertCaseClosedAsync(awayResponse);
        Assert.Equal("Away", away.RfqStatus);

        HttpResponseMessage closedOperation = await trader.PostAsJsonAsync(
            $"/api/rfqs/{hit.CaseId}/ownership/release",
            new { ExpectedVersion = hit.CurrentVersion });
        Assert.Equal(HttpStatusCode.Conflict, closedOperation.StatusCode);

        HttpResponseMessage salesMemoResponse = await sales.PutAsJsonAsync(
            $"/api/rfqs/{hit.CaseId}/memos/sales",
            new { Memo = "customer follow-up", ExpectedVersion = 1 });
        MemoBody salesMemo = await AssertMemoOkAsync(salesMemoResponse);
        Assert.Equal("customer follow-up", salesMemo.Memo);

        HttpResponseMessage forbiddenSalesMemo = await trader.PutAsJsonAsync(
            $"/api/rfqs/{hit.CaseId}/memos/sales",
            new { Memo = "forbidden", ExpectedVersion = salesMemo.Version });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenSalesMemo.StatusCode);

        HttpResponseMessage traderMemoResponse = await trader.PutAsJsonAsync(
            $"/api/rfqs/{hit.CaseId}/memos/trader",
            new { Memo = "desk follow-up", ExpectedVersion = 1 });
        MemoBody traderMemo = await AssertMemoOkAsync(traderMemoResponse);
        Assert.Equal("desk follow-up", traderMemo.Memo);

        HttpResponseMessage correctionResponse = await sales.PostAsJsonAsync(
            $"/api/rfqs/{hit.CaseId}/outcome/correct-to-away",
            new
            {
                Reason = "customer clarification",
                ExpectedCurrentVersion = hit.CurrentVersion,
            });
        CloseRfqBody corrected = await AssertCaseClosedAsync(correctionResponse);
        Assert.Equal("Away", corrected.RfqStatus);
        Assert.Equal(hit.ClosedQuoteId, corrected.ClosedQuoteId);

        HttpResponseMessage handoffResponse = await sales.PutAsJsonAsync(
            $"/api/rfqs/{hit.CaseId}/contact-owner",
            new
            {
                ContactOwnerId = "trader-a",
                ExpectedCurrentVersion = corrected.CurrentVersion,
                Confirmed = true,
            });
        Assert.Equal(HttpStatusCode.Conflict, handoffResponse.StatusCode);

        List<ManagedSalesRfqBody>? salesRows = await sales.GetFromJsonAsync<List<ManagedSalesRfqBody>>(
            "/api/sales-rfqs");
        ManagedSalesRfqBody closedSalesRow = Assert.Single(salesRows!, item => item.CaseId == hit.CaseId);
        Assert.Equal("Away", closedSalesRow.RfqStatus);
        Assert.Null(closedSalesRow.CurrentQuoteId);
        Assert.Equal(hit.ClosedQuoteId, closedSalesRow.ClosedQuoteId);
        Assert.Equal("sales-dev", closedSalesRow.SalesId);
        Assert.True(closedSalesRow.StateSince > DateTimeOffset.MinValue);
        Assert.Equal(99.5m, closedSalesRow.ConfirmedQuote?.Price);
        Assert.Equal("customer follow-up", closedSalesRow.SalesMemo);
        Assert.Equal(salesMemo.Version, closedSalesRow.SalesMemoVersion);

        List<SalesRecentRevisionBody>? recent = await sales.GetFromJsonAsync<List<SalesRecentRevisionBody>>(
            "/api/sales-rfqs/recent-revisions?limit=50");
        Assert.NotNull(recent);
        Assert.DoesNotContain(recent, item => item.CaseId == hit.CaseId);
        Assert.DoesNotContain(recent, item => item.CaseId == away.CaseId);

        List<ManagedTraderRfqBody>? traderRows = await trader.GetFromJsonAsync<List<ManagedTraderRfqBody>>(
            "/api/trader-rfqs");
        ManagedTraderRfqBody closedTraderRow = Assert.Single(traderRows!, item => item.CaseId == hit.CaseId);
        Assert.Equal("Away", closedTraderRow.RfqStatus);
        Assert.Equal("desk follow-up", closedTraderRow.TraderMemo);
        Assert.Equal(traderMemo.Version, closedTraderRow.TraderMemoVersion);
    }

    [Fact]
    public async Task MasterSearchAndDefaultsEndpointsReturnSeededData()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        List<SecurityBody>? securities = await client.GetFromJsonAsync<List<SecurityBody>>(
            "/api/rfqs/candidates/securities?q=375-1");
        List<ClientBody>? clients = await client.GetFromJsonAsync<List<ClientBody>>(
            "/api/rfqs/candidates/clients?q=C001");
        List<UserBody>? users = await client.GetFromJsonAsync<List<UserBody>>(
            "/api/assignable-traders");
        DefaultsBody? defaults = await client.GetFromJsonAsync<DefaultsBody>(
            "/api/rfqs/creation-context?securityId=sec-jgb-375");
        BusinessDateBody? businessDate = await client.GetFromJsonAsync<BusinessDateBody>("/api/business-date");

        Assert.Equal("sec-jgb-375", Assert.Single(securities!).SecurityId);
        Assert.Equal("client-001", Assert.Single(clients!).ClientId);
        Assert.Equal(2, users!.Count);
        Assert.NotNull(defaults);
        Assert.Equal("JGB", defaults.CategoryId);
        Assert.Equal("trader-a", defaults.DefaultAssignedTraderId);
        Assert.Equal(new DateOnly(2026, 9, 23), defaults.StandardSettlementDate);
        Assert.Equal(new DateOnly(2026, 9, 21), businessDate?.Date);
    }

    [Fact]
    public async Task ThemeSettingDefaultsPersistsValidValuesAndIsScopedPerUser()
    {
        using HttpClient traderA = CreateClient("trader-a");
        using HttpClient traderB = CreateClient("trader-b");

        Assert.Equal(
            "Dark",
            (await traderB.GetFromJsonAsync<ThemeBody>(
                "/api/me/settings/theme"))?.Mode);

        HttpResponseMessage lightResponse = await traderA.PutAsJsonAsync(
            "/api/me/settings/theme",
            new { Mode = "Light" });
        Assert.Equal(HttpStatusCode.OK, lightResponse.StatusCode);
        Assert.Equal(
            "Light",
            (await lightResponse.Content.ReadFromJsonAsync<ThemeBody>())?.Mode);
        Assert.Equal(
            "Light",
            (await traderA.GetFromJsonAsync<ThemeBody>(
                "/api/me/settings/theme"))?.Mode);
        Assert.Equal(
            "Dark",
            (await traderB.GetFromJsonAsync<ThemeBody>(
                "/api/me/settings/theme"))?.Mode);

        HttpResponseMessage darkResponse = await traderA.PutAsJsonAsync(
            "/api/me/settings/theme",
            new { Mode = "Dark" });
        Assert.Equal(HttpStatusCode.OK, darkResponse.StatusCode);
        Assert.Equal(
            "Dark",
            (await darkResponse.Content.ReadFromJsonAsync<ThemeBody>())?.Mode);

        HttpResponseMessage invalidResponse = await traderA.PutAsJsonAsync(
            "/api/me/settings/theme",
            new { Mode = "System" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task Feature_contracts_use_typed_events_tagged_expiry_json_grid_and_created_filters()
    {
        using HttpClient client = CreateClient("sales-dev");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/rfqs/drafts/confirm",
            DraftRequest("client-001", "sec-jgb-375", notional: 100_000_000m));
        InitialRfqBody created = await AssertCreatedAsync(createResponse);

        using var eventDocument = JsonDocument.Parse(
            await client.GetStringAsync("/api/events?after=0"));
        JsonElement revisionEvent = eventDocument.RootElement.EnumerateArray().Single(item =>
            item.GetProperty("caseId").GetInt64() == created.CaseId
            && item.GetProperty("type").GetString() == "rfqRevisionConfirmed");
        Assert.False(revisionEvent.TryGetProperty("payloadJson", out _));

        HttpResponseMessage expiryResponse = await client.PutAsJsonAsync(
            "/api/me/settings/quote-expiry",
            new { Type = "None", Minutes = (int?)null });
        Assert.Equal(HttpStatusCode.OK, expiryResponse.StatusCode);
        QuoteExpiryBody? expiry = await expiryResponse.Content.ReadFromJsonAsync<QuoteExpiryBody>();
        Assert.Equal("None", expiry?.Type);
        Assert.Null(expiry?.Minutes);

        HttpResponseMessage modeResponse = await client.PutAsJsonAsync(
            "/api/me/settings/default-quote-mode", new { Mode = "Manual" });
        Assert.Equal(HttpStatusCode.OK, modeResponse.StatusCode);
        Assert.Equal(
            "Manual",
            (await modeResponse.Content
                .ReadFromJsonAsync<QuoteModeBody>())?.Mode);
        Assert.Equal(
            "Manual",
            (await client.GetFromJsonAsync<QuoteModeBody>(
                "/api/me/settings/default-quote-mode"))?.Mode);

        HttpResponseMessage gridResponse = await client.PutAsJsonAsync(
            "/api/me/grid-configs/sales/main",
            new { Version = 1, Config = new { Columns = Array.Empty<object>() } });
        Assert.Equal(HttpStatusCode.OK, gridResponse.StatusCode);
        using var gridDocument = JsonDocument.Parse(await gridResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            JsonValueKind.Object,
            gridDocument.RootElement.GetProperty("config").ValueKind);
        Assert.Equal(
            JsonValueKind.Array,
            gridDocument.RootElement.GetProperty("config").GetProperty("columns").ValueKind);

        var tokyoDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            created.CreatedAt, TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo")).DateTime);
        RfqSearchBody? search = await client.GetFromJsonAsync<RfqSearchBody>(
            $"/api/rfqs/search?createdFrom={tokyoDate:yyyy-MM-dd}&createdTo={tokyoDate:yyyy-MM-dd}");
        Assert.Contains(search!.Items, item => item.CaseId == created.CaseId);
    }

    private static object DraftRequest(
        string clientId,
        string securityId,
        string assignedTraderId = "trader-a",
        decimal? notional = null) => new
        {
            ClientId = clientId,
            SecurityId = securityId,
            Notional = notional,
            SettlementDate = "2026-09-24",
            StandardSettlementDate = "2026-09-23",
            SalesAndTradingMessage = "",
            AssignedTraderId = assignedTraderId,
        };

    private static async Task<InitialRfqBody> AssertCreatedAsync(HttpResponseMessage response)
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<InitialRfqBody>(
            await response.Content.ReadFromJsonAsync<InitialRfqBody>());
    }

    private HttpClient CreateClient(string userId)
    {
        HttpClient client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Development-User", userId);
        return client;
    }

    private static async Task<OwnershipBody> AssertOwnershipOkAsync(
        HttpResponseMessage response)
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<OwnershipBody>(
            await response.Content.ReadFromJsonAsync<OwnershipBody>());
    }

    private static async Task<WorkingQuoteBody> AssertWorkingQuoteOkAsync(
        HttpResponseMessage response)
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<WorkingQuoteBody>(
            await response.Content.ReadFromJsonAsync<WorkingQuoteBody>());
    }

    private static async Task<ConfirmQuoteBody> AssertQuoteConfirmedAsync(
        HttpResponseMessage response)
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<ConfirmQuoteBody>(
            await response.Content.ReadFromJsonAsync<ConfirmQuoteBody>());
    }

    private static async Task<PresentationBody> AssertPresentationOkAsync(
        HttpResponseMessage response)
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<PresentationBody>(
            await response.Content.ReadFromJsonAsync<PresentationBody>());
    }

    private static async Task<ConfirmQuoteBody> CreateQuotedCaseAsync(
        HttpClient sales,
        HttpClient trader,
        string clientId)
    {
        HttpResponseMessage createResponse = await sales.PostAsJsonAsync(
            "/api/rfqs/drafts/confirm",
            new
            {
                ClientId = clientId,
                SecurityId = "sec-jgb-375",
                Notional = 100_000_000m,
                SettlementDate = "2026-09-24",
                StandardSettlementDate = "2026-09-23",
                SalesAndTradingMessage = "",
                AssignedTraderId = "trader-a",
            });
        InitialRfqBody created = await AssertCreatedAsync(createResponse);
        List<TraderRfqBody>? rows = await trader.GetFromJsonAsync<List<TraderRfqBody>>(
            "/api/trader-rfqs");
        TraderRfqBody row = Assert.Single(rows!, item => item.CaseId == created.CaseId);
        HttpResponseMessage pickUpResponse = await trader.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/ownership/pick-up",
            new { row.ExpectedVersion, Confirmed = false });
        OwnershipBody pickedUp = await AssertOwnershipOkAsync(pickUpResponse);
        HttpResponseMessage calculateResponse = await trader.PutAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/working-quote/calculate",
            new
            {
                Driver = "Price",
                Value = 99.5m,
                SimpleYieldSlide = 0.03m,
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = row.WorkingQuoteVersion,
            });
        WorkingQuoteBody calculated = await AssertWorkingQuoteOkAsync(calculateResponse);
        HttpResponseMessage confirmResponse = await trader.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/quote/confirm",
            new
            {
                Expiry = new { Type = "None", Minutes = (int?)null },
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = calculated.Version,
            });
        return await AssertQuoteConfirmedAsync(confirmResponse);
    }

    private static async Task<CloseRfqBody> AssertCaseClosedAsync(
        HttpResponseMessage response)
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<CloseRfqBody>(
            await response.Content.ReadFromJsonAsync<CloseRfqBody>());
    }

    private static async Task<MemoBody> AssertMemoOkAsync(HttpResponseMessage response)
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<MemoBody>(
            await response.Content.ReadFromJsonAsync<MemoBody>());
    }

    private sealed record InitialRfqBody(
        long CaseId,
        Guid RevisionId,
        string RfqStatus,
        string RevisionStatus,
        string? QuoteStatus,
        string? QuoteRequestReason,
        string CategoryId,
        string ContactOwnerId,
        string AssignedTraderId,
        decimal? Notional,
        DateOnly? SettlementDate,
        DateOnly StandardSettlementDate,
        string SalesAndTradingMessage,
        long Version,
        DateTimeOffset CreatedAt);

    private sealed record SalesRfqBody(
        long CaseId,
        string ClientName,
        string SecurityJapaneseName,
        string SecurityBbgDisplay,
        string RfqStatus,
        string RevisionStatus,
        string? QuoteStatus,
        string? QuoteRequestReason);

    private sealed record TraderRfqBody(
        long CaseId,
        string AssignedTraderId,
        bool Owned,
        long CurrentVersion,
        long WorkingQuoteVersion,
        string SalesAndTradingMessage,
        DateTimeOffset StateSince)
    {
        public long ExpectedVersion => CurrentVersion;
    }

    private sealed record OwnershipBody(
        long CaseId,
        string AssignedTraderId,
        bool Owned,
        long CurrentVersion);

    private sealed record WorkingQuoteBody(
        long CaseId,
        Guid RevisionId,
        string Mode,
        CalculatedQuoteBody? Calculated,
        ManualQuoteBody? Manual,
        long Version,
        long CurrentVersion);

    private sealed record CalculatedQuoteBody(
        string Driver,
        decimal DriverValue,
        decimal Price,
        decimal BbgYield,
        decimal BaseSimpleYield,
        decimal SimpleYieldSlide,
        decimal FinalSimpleYield,
        decimal InternalYield,
        decimal GSpread,
        decimal Asw,
        decimal Ysc,
        decimal ISpread,
        decimal ZSpread);

    private sealed record ManualQuoteBody(
        decimal? Price,
        decimal? FinalSimpleYield);

    private sealed record QuoteExpiryBody(string Type, int? Minutes);
    private sealed record QuoteModeBody(string Mode);

    private sealed record ThemeBody(string Mode);

    private sealed record RfqSearchBody(
        IReadOnlyList<RfqSearchItemBody> Items,
        bool RequiresNarrowing);

    private sealed record RfqSearchItemBody(long CaseId);

    private sealed record ConfirmQuoteBody(
        long CaseId,
        Guid QuoteId,
        Guid RevisionId,
        string RfqStatus,
        string QuoteStatus,
        string Mode,
        CalculatedQuoteBody? Calculated,
        ManualQuoteBody? Manual,
        QuoteExpiryBody Expiry,
        DateTimeOffset? ConfirmedAt,
        DateTimeOffset? ExpiresAt,
        long CurrentVersion);

    private sealed record PresentationBody(
        long CaseId,
        Guid QuoteId,
        string RfqStatus,
        string QuoteStatus,
        long CurrentVersion);

    private sealed record CloseRfqBody(
        long CaseId,
        string RfqStatus,
        Guid ClosedQuoteId,
        bool Owned,
        long CurrentVersion);

    private sealed record MemoBody(long CaseId, string Memo, long Version);

    private sealed record ManagedSalesRfqBody(
        long CaseId,
        string RfqStatus,
        Guid? CurrentQuoteId,
        Guid? ClosedQuoteId,
        string? SalesId,
        DateTimeOffset StateSince,
        SalesQuoteSummaryBody? ConfirmedQuote,
        string SalesMemo,
        long SalesMemoVersion);

    private sealed record SalesQuoteSummaryBody(decimal? Price);
    private sealed record SalesRecentRevisionBody(long CaseId);

    private sealed record ManagedTraderRfqBody(
        long CaseId,
        string RfqStatus,
        string TraderMemo,
        long TraderMemoVersion);

    private sealed record SecurityBody(string SecurityId);
    private sealed record ClientBody(string ClientId);
    private sealed record UserBody(string UserId);

    private sealed record DefaultsBody(
        string CategoryId,
        string DefaultAssignedTraderId,
        DateOnly StandardSettlementDate);

    private sealed record BusinessDateBody(DateOnly Date);
}
