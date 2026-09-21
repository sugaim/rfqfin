using System.Net;
using System.Net.Http.Json;
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
        using var client = fixture.Factory.CreateClient();

        var saveResponse = await client.PostAsJsonAsync(
            "/api/rfqs",
            new { ClientId = "client-001", SecurityId = "sec-jgb-375" });
        var saved = await AssertCreatedAsync(saveResponse);
        Assert.Equal("Draft", saved.RfqStatus);
        Assert.Equal("Draft", saved.RevisionStatus);
        Assert.Null(saved.Notional);
        Assert.Null(saved.SettlementDate);
        Assert.Null(saved.QuoteStatus);
        Assert.Equal(1, saved.Version);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/rfqs/{saved.CaseId}/draft",
            new
            {
                Notional = 100_000_000m,
                SettlementDate = "2026-09-24",
                SalesAndTradingMessage = "Please quote",
                AssignedTraderId = "trader-a",
                ExpectedVersion = saved.Version,
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<InitialRfqBody>();
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Version);
        Assert.Equal(100_000_000m, updated.Notional);

        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/rfqs/{saved.CaseId}/confirm",
            new
            {
                updated.Notional,
                SettlementDate = "2026-09-24",
                updated.SalesAndTradingMessage,
                updated.AssignedTraderId,
                ExpectedVersion = updated.Version,
            });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<InitialRfqBody>();
        Assert.NotNull(confirmed);
        Assert.Equal("Active", confirmed.RfqStatus);
        Assert.Equal("Confirmed", confirmed.RevisionStatus);
        Assert.Equal("Requested", confirmed.QuoteStatus);
        Assert.Equal("Initial", confirmed.QuoteRequestReason);
        Assert.Equal(3, confirmed.Version);

        var directResponse = await client.PostAsJsonAsync(
            "/api/rfqs/confirm",
            new
            {
                ClientId = "client-002",
                SecurityId = "sec-toyota-1",
                Notional = 50_000_000m,
                SettlementDate = "2026-09-24",
                SalesAndTradingMessage = "Direct confirmation",
                AssignedTraderId = "trader-b",
            });
        var directlyConfirmed = await AssertCreatedAsync(directResponse);
        Assert.Equal(saved.CaseId + 1, directlyConfirmed.CaseId);
        Assert.Equal("Active", directlyConfirmed.RfqStatus);
        Assert.Equal("Confirmed", directlyConfirmed.RevisionStatus);
        Assert.Equal("Requested", directlyConfirmed.QuoteStatus);
        Assert.Equal("Initial", directlyConfirmed.QuoteRequestReason);

        var discardSaveResponse = await client.PostAsJsonAsync(
            "/api/rfqs",
            new { ClientId = "client-003", SecurityId = "sec-jgb-375" });
        var toDiscard = await AssertCreatedAsync(discardSaveResponse);
        var discardResponse = await client.PostAsJsonAsync(
            $"/api/rfqs/{toDiscard.CaseId}/discard",
            new { ExpectedVersion = toDiscard.Version });
        Assert.Equal(HttpStatusCode.NoContent, discardResponse.StatusCode);

        var rows = await client.GetFromJsonAsync<List<SalesRfqBody>>(
            "/api/rfqs/active-sales");
        var list = Assert.IsType<List<SalesRfqBody>>(rows);
        var confirmedRow = Assert.Single(list, item => item.CaseId == saved.CaseId);
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
        using var client = fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/rfqs/confirm",
            new { ClientId = "client-004", SecurityId = "sec-jgb-375" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TraderOwnershipWorkflowSupportsRoutingPermissionsAndConcurrency()
    {
        using var sales = CreateClient("sales-dev");
        var createResponse = await sales.PostAsJsonAsync(
            "/api/rfqs/confirm",
            new
            {
                ClientId = "client-001",
                SecurityId = "sec-jgb-375",
                Notional = 100_000_000m,
                SettlementDate = "2026-09-24",
                AssignedTraderId = "trader-a",
            });
        var created = await AssertCreatedAsync(createResponse);

        using var traderA = CreateClient("trader-a");
        var rows = await traderA.GetFromJsonAsync<List<TraderRfqBody>>(
            "/api/trader/rfqs/active");
        var row = Assert.Single(rows!, item => item.CaseId == created.CaseId);
        Assert.False(row.Owned);
        Assert.Equal("trader-a", row.AssignedTraderId);

        var pickUp = await traderA.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/pick-up",
            new { row.ExpectedVersion, Confirmed = false });
        var pickedUp = await AssertOwnershipOkAsync(pickUp);
        Assert.True(pickedUp.Owned);
        Assert.Equal("trader-a", pickedUp.AssignedTraderId);

        var calculateResponse = await traderA.PutAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/working-quote/calculate",
            new
            {
                Driver = "Price",
                Value = 99.5m,
                SimpleYieldSlide = 0.03m,
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = row.WorkingQuoteVersion,
            });
        var calculated = await AssertWorkingQuoteOkAsync(calculateResponse);
        Assert.Equal("Calculated", calculated.Mode);
        Assert.Equal(99.5m, calculated.Calculated?.Price);
        Assert.Equal(
            calculated.Calculated?.BaseSimpleYield + 0.03m,
            calculated.Calculated?.FinalSimpleYield);

        var failedCalculation = await traderA.PutAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/working-quote/calculate",
            new
            {
                Driver = "Price",
                Value = -999m,
                SimpleYieldSlide = 0m,
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = calculated.Version,
            });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failedCalculation.StatusCode);
        await using (var failureScope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = failureScope.ServiceProvider.GetRequiredService<RfqDbContext>();
            var failureCount = await dbContext.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM calculation_failure_logs
                WHERE case_id = {created.CaseId}
                """).SingleAsync();
            Assert.Equal(1, failureCount);
        }

        var manualModeResponse = await traderA.PutAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/working-quote/mode",
            new
            {
                Mode = "Manual",
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = calculated.Version,
            });
        var manualMode = await AssertWorkingQuoteOkAsync(manualModeResponse);
        Assert.Equal("Manual", manualMode.Mode);
        Assert.Null(manualMode.Manual?.Price);
        Assert.Equal(calculated.Calculated, manualMode.Calculated);

        var manualUpdateResponse = await traderA.PutAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/working-quote/manual",
            new
            {
                Price = 98.75m,
                FinalSimpleYield = 1.25m,
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = manualMode.Version,
            });
        var manualUpdate = await AssertWorkingQuoteOkAsync(manualUpdateResponse);
        Assert.Equal(98.75m, manualUpdate.Manual?.Price);
        Assert.Equal(1.25m, manualUpdate.Manual?.FinalSimpleYield);

        var calculatedModeResponse = await traderA.PutAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/working-quote/mode",
            new
            {
                Mode = "Calculated",
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = manualUpdate.Version,
            });
        var restoredCalculated = await AssertWorkingQuoteOkAsync(calculatedModeResponse);
        Assert.Equal(calculated.Calculated, restoredCalculated.Calculated);

        var confirmQuoteResponse = await traderA.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/confirm-quote",
            new
            {
                ExpiryMinutes = 5,
                ExpectedCurrentVersion = pickedUp.CurrentVersion,
                ExpectedWorkingQuoteVersion = restoredCalculated.Version,
            });
        var confirmedQuote = await AssertQuoteConfirmedAsync(confirmQuoteResponse);
        Assert.Equal("Quoted", confirmedQuote.QuoteStatus);
        Assert.Equal("Active", confirmedQuote.RfqStatus);
        Assert.Equal(restoredCalculated.Calculated, confirmedQuote.Calculated);
        Assert.Equal(confirmedQuote.ExpiresAt, confirmedQuote.ConfirmedAt?.AddMinutes(5));

        var secondConfirm = await traderA.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/confirm-quote",
            new
            {
                ExpiryMinutes = 5,
                ExpectedCurrentVersion = confirmedQuote.CurrentVersion,
                ExpectedWorkingQuoteVersion = restoredCalculated.Version,
            });
        Assert.Equal(HttpStatusCode.Conflict, secondConfirm.StatusCode);

        var editQuoted = await traderA.PutAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/working-quote/calculate",
            new
            {
                Driver = "Price",
                Value = 98m,
                SimpleYieldSlide = 0m,
                ExpectedCurrentVersion = confirmedQuote.CurrentVersion,
                ExpectedWorkingQuoteVersion = restoredCalculated.Version,
            });
        Assert.Equal(HttpStatusCode.Conflict, editQuoted.StatusCode);

        var forbiddenPresent = await traderA.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/present",
            new { ExpectedCurrentVersion = confirmedQuote.CurrentVersion });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenPresent.StatusCode);

        var presentResponse = await sales.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/present",
            new { ExpectedCurrentVersion = confirmedQuote.CurrentVersion });
        var presented = await AssertPresentationOkAsync(presentResponse);
        Assert.Equal("Presented", presented.RfqStatus);
        Assert.Equal("Quoted", presented.QuoteStatus);

        var unpresentResponse = await sales.PostAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/unpresent",
            new { ExpectedCurrentVersion = presented.CurrentVersion });
        var unpresented = await AssertPresentationOkAsync(unpresentResponse);
        Assert.Equal("Active", unpresented.RfqStatus);
        Assert.Equal(confirmedQuote.QuoteId, unpresented.QuoteId);

        var release = await traderA.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/release",
            new { ExpectedVersion = unpresented.CurrentVersion });
        var released = await AssertOwnershipOkAsync(release);
        Assert.False(released.Owned);

        var assign = await traderA.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/assign",
            new { TargetTraderId = "trader-b", ExpectedVersion = released.CurrentVersion });
        var assigned = await AssertOwnershipOkAsync(assign);
        Assert.Equal("trader-b", assigned.AssignedTraderId);
        Assert.False(assigned.Owned);

        var missingConfirmation = await traderA.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/pick-up",
            new { ExpectedVersion = assigned.CurrentVersion, Confirmed = false });
        Assert.Equal(HttpStatusCode.BadRequest, missingConfirmation.StatusCode);

        using var traderB = CreateClient("trader-b");
        var ownerBResponse = await traderB.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/pick-up",
            new { ExpectedVersion = assigned.CurrentVersion, Confirmed = false });
        var ownerB = await AssertOwnershipOkAsync(ownerBResponse);
        Assert.True(ownerB.Owned);

        var forbiddenRelease = await traderA.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/release",
            new { ExpectedVersion = ownerB.CurrentVersion });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRelease.StatusCode);

        var takeOverResponse = await traderA.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/take-over",
            new { ExpectedVersion = ownerB.CurrentVersion, Confirmed = true });
        var takenOver = await AssertOwnershipOkAsync(takeOverResponse);
        Assert.True(takenOver.Owned);
        Assert.Equal("trader-a", takenOver.AssignedTraderId);

        var staleTakeOver = await traderB.PostAsJsonAsync(
            $"/api/trader/rfqs/{created.CaseId}/take-over",
            new { ExpectedVersion = ownerB.CurrentVersion, Confirmed = true });
        Assert.Equal(HttpStatusCode.Conflict, staleTakeOver.StatusCode);

        var forbiddenCreate = await traderA.PostAsJsonAsync(
            "/api/rfqs",
            new { ClientId = "client-001", SecurityId = "sec-jgb-375" });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);
    }

    [Fact]
    public async Task RevisionChangesRequireContactOwnerThroughCentralAuthorization()
    {
        using var owner = CreateClient("sales-dev");
        var createResponse = await owner.PostAsJsonAsync(
            "/api/rfqs",
            new { ClientId = "client-004", SecurityId = "sec-other-1" });
        var draft = await AssertCreatedAsync(createResponse);

        using var otherSales = CreateClient("sales-a");
        var response = await otherSales.PutAsJsonAsync(
            $"/api/rfqs/{draft.CaseId}/draft",
            new
            {
                Notional = 1_000_000m,
                SettlementDate = "2026-09-24",
                ExpectedVersion = draft.Version,
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MasterSearchAndDefaultsEndpointsReturnSeededData()
    {
        using var client = fixture.Factory.CreateClient();

        var securities = await client.GetFromJsonAsync<List<SecurityBody>>(
            "/api/masters/securities/search?q=375-1");
        var clients = await client.GetFromJsonAsync<List<ClientBody>>(
            "/api/masters/clients/search?q=C001");
        var users = await client.GetFromJsonAsync<List<UserBody>>(
            "/api/masters/users?role=Trader");
        var defaults = await client.GetFromJsonAsync<DefaultsBody>(
            "/api/rfq-defaults?securityId=sec-jgb-375");
        var systemDate = await client.GetFromJsonAsync<SystemDateBody>("/api/system-date");

        Assert.Equal("sec-jgb-375", Assert.Single(securities!).SecurityId);
        Assert.Equal("client-001", Assert.Single(clients!).ClientId);
        Assert.Equal(2, users!.Count);
        Assert.NotNull(defaults);
        Assert.Equal("JGB", defaults.CategoryId);
        Assert.Equal("sales-dev", defaults.ContactOwnerId);
        Assert.Equal("trader-a", defaults.AssignedTraderId);
        Assert.Equal(new DateOnly(2026, 9, 23), defaults.StandardSettlementDate);
        Assert.Equal(new DateOnly(2026, 9, 21), systemDate?.Date);
    }

    private static async Task<InitialRfqBody> AssertCreatedAsync(HttpResponseMessage response)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<InitialRfqBody>(
            await response.Content.ReadFromJsonAsync<InitialRfqBody>());
    }

    private HttpClient CreateClient(string userId)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Development-User", userId);
        return client;
    }

    private static async Task<OwnershipBody> AssertOwnershipOkAsync(
        HttpResponseMessage response)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<OwnershipBody>(
            await response.Content.ReadFromJsonAsync<OwnershipBody>());
    }

    private static async Task<WorkingQuoteBody> AssertWorkingQuoteOkAsync(
        HttpResponseMessage response)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<WorkingQuoteBody>(
            await response.Content.ReadFromJsonAsync<WorkingQuoteBody>());
    }

    private static async Task<ConfirmQuoteBody> AssertQuoteConfirmedAsync(
        HttpResponseMessage response)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<ConfirmQuoteBody>(
            await response.Content.ReadFromJsonAsync<ConfirmQuoteBody>());
    }

    private static async Task<PresentationBody> AssertPresentationOkAsync(
        HttpResponseMessage response)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but received {(int)response.StatusCode}: {responseBody}");
        return Assert.IsType<PresentationBody>(
            await response.Content.ReadFromJsonAsync<PresentationBody>());
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
        long WorkingQuoteVersion)
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
        decimal Asw);

    private sealed record ManualQuoteBody(
        decimal? Price,
        decimal? FinalSimpleYield);

    private sealed record ConfirmQuoteBody(
        long CaseId,
        Guid QuoteId,
        Guid RevisionId,
        string RfqStatus,
        string QuoteStatus,
        string Mode,
        CalculatedQuoteBody? Calculated,
        ManualQuoteBody? Manual,
        int? ExpiryMinutes,
        DateTimeOffset? ConfirmedAt,
        DateTimeOffset? ExpiresAt,
        long CurrentVersion);

    private sealed record PresentationBody(
        long CaseId,
        Guid QuoteId,
        string RfqStatus,
        string QuoteStatus,
        long CurrentVersion);

    private sealed record SecurityBody(string SecurityId);
    private sealed record ClientBody(string ClientId);
    private sealed record UserBody(string UserId);
    private sealed record DefaultsBody(
        string CategoryId,
        string ContactOwnerId,
        string AssignedTraderId,
        DateOnly StandardSettlementDate);
    private sealed record SystemDateBody(DateOnly Date);
}
