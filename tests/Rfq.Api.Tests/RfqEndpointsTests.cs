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
            new { ClientId = "client-api", SecurityId = "security-api" });

        var postResponseBody = await postResponse.Content.ReadAsStringAsync();
        Assert.True(
            postResponse.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)postResponse.StatusCode}: {postResponseBody}");
        var created = await postResponse.Content.ReadFromJsonAsync<CreateDraftBody>();
        Assert.NotNull(created);
        Assert.True(created.CaseId > 0);
        Assert.NotEqual(Guid.Empty, created.RevisionId);
        Assert.Equal("Draft", created.RfqStatus);

        var secondPostResponse = await client.PostAsJsonAsync(
            "/api/rfqs",
            new { ClientId = "client-api-2", SecurityId = "security-api-2" });
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
        Assert.Equal("client-api", row.ClientId);
        Assert.Equal("security-api", row.SecurityId);
        Assert.Equal("Draft", row.RfqStatus);
        Assert.Equal("Draft", row.RevisionStatus);
        Assert.Contains(list, item => item.CaseId == secondCreated.CaseId);
    }

    private sealed record CreateDraftBody(
        long CaseId,
        Guid RevisionId,
        string RfqStatus,
        DateTimeOffset CreatedAt);

    private sealed record SalesRfqBody(
        long CaseId,
        string ClientId,
        string SecurityId,
        string RfqStatus,
        Guid CurrentRevisionId,
        string RevisionStatus,
        DateTimeOffset CreatedAt);
}
