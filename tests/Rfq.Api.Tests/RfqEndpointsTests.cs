using System.Net;
using System.Net.Http.Json;
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
