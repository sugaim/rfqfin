using System.Text.Json;
using Xunit;

namespace Rfq.Api.Tests;

public sealed class OpenApiContractTests(RfqApiFixture fixture)
    : IClassFixture<RfqApiFixture>
{
    private static readonly string[] RequiredOperationIds =
    [
        "CreateDraft", "ConfirmNewRfq", "UpdateInitialDraft", "CreateFromExisting",
        "StartAmendment", "SaveAmendment", "CloseHitRfq", "CorrectOutcomeToHit",
        "CorrectOutcomeToAway", "UpdateSalesMemo", "UpdateTraderMemo",
        "CalculateWorkingQuote", "ChangeWorkingQuoteMode", "UpdateManualWorkingQuote",
        "GetRfqQuoteHistory", "ResolveRfqCreationContext", "ConfirmInitialDrafts",
        "DiscardInitialDrafts", "ConfirmAmendments", "DiscardAmendments", "ConfirmQuotes",
        "WithdrawQuotes", "PresentRfqs", "UnpresentRfqs", "CloseAwayRfqs", "CancelRfqs",
        "ReopenRfqs", "PickUpRfqs", "ReleaseRfqs", "AssignTraders", "TakeOverRfqs",
        "ChangeContactOwners", "GetActiveSalesRfqs", "GetSalesRecentRevisions",
        "GetActiveTraderRfqs", "StreamWorklistInvalidations", "SearchRfqs",
        "GetAssignableTraders", "GetContactOwnerCandidates", "SearchClients",
        "SearchSecurities", "GetPostProcess", "CommitPostProcessChanges", "GetMe",
        "GetQuoteExpiry", "SaveQuoteExpiry", "GetDefaultQuoteMode", "SaveDefaultQuoteMode",
        "GetTheme", "SaveTheme", "GetGridConfig", "SaveGridConfig", "GetBusinessDate",
        "ScratchPrice", "GetHealth", "GetReadiness",
    ];

    [Fact]
    public async Task Generated_contract_has_stable_operations_routes_and_shared_schemas()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using var document = JsonDocument.Parse(
            await client.GetStringAsync("/openapi/v1.json"));
        JsonElement paths = document.RootElement.GetProperty("paths");
        var operationIds = new List<string>();

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            foreach (JsonProperty method in path.Value.EnumerateObject())
            {
                Assert.True(method.Value.TryGetProperty("operationId", out JsonElement operationId));
                operationIds.Add(operationId.GetString()!);
            }
        }

        Assert.Equal(operationIds.Count, operationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(RequiredOperationIds, required => Assert.Contains(required, operationIds));
        Assert.DoesNotContain(
            paths.EnumerateObject(),
            path =>
                path.Name is "/api/events" or "/api/eod" or "/api/category-routings"
                || path.Name.EndsWith("/revisions", StringComparison.Ordinal));
        Assert.DoesNotContain(
            paths.EnumerateObject(),
            path =>
                path.Name.StartsWith("/api/rfqs/", StringComparison.Ordinal)
                && path.Name.Contains("bulk", StringComparison.OrdinalIgnoreCase));

        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("ApiProblemDetails", out _));
        Assert.True(schemas.TryGetProperty("RfqStatus", out _));
        Assert.True(schemas.TryGetProperty("QuoteRequestReason", out _));
        JsonProperty quoteStatus = Assert.Single(
            schemas.EnumerateObject(),
            schema => schema.Name.EndsWith("QuoteStatus", StringComparison.Ordinal));
        Assert.Equal("string", quoteStatus.Value.GetProperty("type").GetString());
        Assert.Equal(
            ["Requested", "Quoted"],
            quoteStatus.Value.GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            ["Applied", "NoChange", "Failed"],
            schemas.GetProperty("CaseOperationStatus").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()));
        Assert.DoesNotContain(
            schemas.EnumerateObject(),
            schema =>
                schema.Name.EndsWith("StatusValue", StringComparison.Ordinal)
                || schema.Name.EndsWith("ReasonValue", StringComparison.Ordinal));
    }
}
