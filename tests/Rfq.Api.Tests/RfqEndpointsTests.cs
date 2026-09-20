using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Rfq.Api.Tests;

public sealed class RfqEndpointsTests(RfqApiFixture fixture)
    : IClassFixture<RfqApiFixture>
{
    [Fact]
    public async Task PostCreateThenGetListReturnsPersistedDraft()
    {
        using var client = fixture.Factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync(
            "/api/rfqs",
            new { ClientId = "client-api", SecurityId = "security-api" });

        var postResponseBody = await postResponse.Content.ReadAsStringAsync();
        Assert.True(
            postResponse.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)postResponse.StatusCode}: {postResponseBody}");
        var created = await postResponse.Content.ReadFromJsonAsync<CreateDraftBody>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.CaseId);
        Assert.NotEqual(Guid.Empty, created.RevisionId);
        Assert.Equal("Draft", created.RfqStatus);

        var rows = await client.GetFromJsonAsync<List<SalesRfqBody>>(
            "/api/rfqs/active-sales");

        var row = Assert.Single(Assert.IsType<List<SalesRfqBody>>(rows));
        Assert.Equal(created.CaseId, row.CaseId);
        Assert.Equal("client-api", row.ClientId);
        Assert.Equal("security-api", row.SecurityId);
        Assert.Equal("Draft", row.RfqStatus);
        Assert.Equal("Draft", row.RevisionStatus);
    }

    private sealed record CreateDraftBody(
        Guid CaseId,
        Guid RevisionId,
        string RfqStatus,
        DateTimeOffset CreatedAt);

    private sealed record SalesRfqBody(
        Guid CaseId,
        string ClientId,
        string SecurityId,
        string RfqStatus,
        Guid CurrentRevisionId,
        string RevisionStatus,
        DateTimeOffset CreatedAt);
}
