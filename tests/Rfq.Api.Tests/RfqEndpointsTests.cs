using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Rfq.Api.Tests;

public sealed class RfqEndpointsTests(RfqApiFixture fixture)
    : IClassFixture<RfqApiFixture>
{
    [Fact]
    public async Task PostTwoDraftsReturnsIncrementingIdsAndListsBoth()
    {
        using var client = fixture.Factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync(
            "/api/rfqs",
            new
            {
                ClientId = "client-001",
                SecurityId = "sec-jgb-375",
                SettlementDate = "2026-09-24",
                AssignedTraderId = "trader-a",
            });

        var postResponseBody = await postResponse.Content.ReadAsStringAsync();
        Assert.True(
            postResponse.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)postResponse.StatusCode}: {postResponseBody}");
        var created = await postResponse.Content.ReadFromJsonAsync<CreateDraftBody>();
        Assert.NotNull(created);
        Assert.True(created.CaseId > 0);
        Assert.NotEqual(Guid.Empty, created.RevisionId);
        Assert.Equal("Draft", created.RfqStatus);
        Assert.Equal("JGB", created.CategoryId);
        Assert.Equal("sales-dev", created.ContactOwnerId);
        Assert.Equal("trader-a", created.AssignedTraderId);

        var secondPostResponse = await client.PostAsJsonAsync(
            "/api/rfqs",
            new
            {
                ClientId = "client-002",
                SecurityId = "sec-toyota-1",
                SettlementDate = "2026-09-24",
                AssignedTraderId = "trader-b",
            });
        Assert.Equal(HttpStatusCode.Created, secondPostResponse.StatusCode);
        var secondCreated = await secondPostResponse.Content.ReadFromJsonAsync<CreateDraftBody>();
        Assert.NotNull(secondCreated);
        Assert.Equal(created.CaseId + 1, secondCreated.CaseId);

        var rows = await client.GetFromJsonAsync<List<SalesRfqBody>>(
            "/api/rfqs/active-sales");

        var list = Assert.IsType<List<SalesRfqBody>>(rows);
        Assert.Equal(2, list.Count);
        var row = Assert.Single(list, item => item.CaseId == created.CaseId);
        Assert.Equal(created.CaseId, row.CaseId);
        Assert.Equal("client-001", row.ClientId);
        Assert.Equal("青空銀行", row.ClientName);
        Assert.Equal("sec-jgb-375", row.SecurityId);
        Assert.Equal("利付国債 第375回", row.SecurityJapaneseName);
        Assert.Equal("JGB 0.5 03/20/2030 #375", row.SecurityBbgDisplay);
        Assert.Equal("JGB", row.CategoryId);
        Assert.Equal("Draft", row.RfqStatus);
        Assert.Equal("Draft", row.RevisionStatus);
        Assert.Contains(list, item => item.CaseId == secondCreated.CaseId);
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

    private sealed record CreateDraftBody(
        long CaseId,
        Guid RevisionId,
        string RfqStatus,
        string CategoryId,
        string ContactOwnerId,
        string AssignedTraderId,
        DateOnly SettlementDate,
        DateOnly StandardSettlementDate,
        DateTimeOffset CreatedAt);

    private sealed record SalesRfqBody(
        long CaseId,
        string ClientId,
        string ClientName,
        string SecurityId,
        string SecurityJapaneseName,
        string SecurityBbgDisplay,
        string CategoryId,
        string RfqStatus,
        Guid CurrentRevisionId,
        string RevisionStatus,
        string ContactOwnerId,
        string AssignedTraderId,
        DateOnly SettlementDate,
        DateOnly StandardSettlementDate,
        DateTimeOffset CreatedAt);

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
