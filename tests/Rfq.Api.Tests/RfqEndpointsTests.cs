using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Rfq.Api.Tests;

public sealed class RfqEndpointsTests(RfqApiFixture fixture)
    : IClassFixture<RfqApiFixture>
{
    [Fact]
    public async Task Automatic_validation_uses_the_common_problem_contract()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/rfqs/drafts/confirm",
            new { ClientId = "client-001", SecurityId = "sec-jgb-375" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        ApiProblemBody problem = (await response.Content.ReadFromJsonAsync<ApiProblemBody>())!;
        Assert.Equal("Validation", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task Initial_draft_single_ui_actions_use_plural_contracts()
    {
        using HttpClient sales = CreateClient("sales-dev");
        InitialRfqBody draft = await CreateDraftAsync(sales, "client-001");

        HttpResponseMessage update = await sales.PutAsJsonAsync(
            $"/api/rfqs/{draft.CaseId}/draft",
            new
            {
                Notional = 100_000_000m,
                SettlementDate = "2026-09-24",
                StandardSettlementDate = "2026-09-23",
                SalesAndTradingMessage = "Please quote",
                AssignedTraderId = "trader-a",
                ExpectedCurrentVersion = draft.Version,
            });
        update.EnsureSuccessStatusCode();
        InitialRfqBody updated = (await update.Content.ReadFromJsonAsync<InitialRfqBody>())!;

        CaseOperationBody confirmed = Assert.Single(await PostOperationsAsync(
            sales,
            "/api/rfqs/confirm-initial-drafts",
            new
            {
                Items = new[]
                {
                    new
                    {
                        draft.CaseId,
                        updated.Notional,
                        SettlementDate = "2026-09-24",
                        StandardSettlementDate = "2026-09-23",
                        updated.SalesAndTradingMessage,
                        updated.AssignedTraderId,
                        ExpectedCurrentVersion = updated.Version,
                    },
                },
            }));
        Assert.Equal("Applied", confirmed.Status);

        List<SalesRfqBody> worklist = (await sales.GetFromJsonAsync<List<SalesRfqBody>>(
            "/api/worklists/sales"))!;
        SalesRfqBody active = Assert.Single(worklist, item => item.CaseId == draft.CaseId);
        Assert.Equal("Active", active.RfqStatus);
        Assert.Equal("Requested", active.QuoteStatus);

        InitialRfqBody discardedDraft = await CreateDraftAsync(sales, "client-003");
        CaseOperationBody discarded = Assert.Single(await PostOperationsAsync(
            sales,
            "/api/rfqs/discard-initial-drafts",
            new
            {
                Items = new[]
                {
                    new
                    {
                        discardedDraft.CaseId,
                        ExpectedCurrentVersion = discardedDraft.Version,
                    },
                },
            }));
        Assert.Equal("Applied", discarded.Status);
    }

    [Fact]
    public async Task Trader_quote_and_sales_presentation_use_one_item_plural_requests()
    {
        using HttpClient sales = CreateClient("sales-dev");
        InitialRfqBody created = await ConfirmNewAsync(sales, "client-002");
        using HttpClient trader = CreateClient("trader-a");
        TraderRfqBody row = await GetTraderRowAsync(trader, created.CaseId);

        CaseOperationBody pickedUp = Assert.Single(await PostOperationsAsync(
            trader,
            "/api/rfqs/pick-up",
            new
            {
                Confirmed = false,
                Items = new[] { new { row.CaseId, ExpectedCurrentVersion = row.CurrentVersion } },
            }));
        Assert.Equal("Applied", pickedUp.Status);
        row = await GetTraderRowAsync(trader, created.CaseId);
        Assert.True(row.Owned);

        HttpResponseMessage calculation = await trader.PutAsJsonAsync(
            $"/api/rfqs/{created.CaseId}/working-quote/calculate",
            new
            {
                Driver = "Price",
                Value = 99.5m,
                SimpleYieldSlide = 0.03m,
                ExpectedCurrentVersion = row.CurrentVersion,
                ExpectedWorkingQuoteVersion = row.WorkingQuoteVersion,
            });
        calculation.EnsureSuccessStatusCode();
        WorkingQuoteBody working = (await calculation.Content
            .ReadFromJsonAsync<WorkingQuoteBody>())!;

        CaseOperationBody quote = Assert.Single(await PostOperationsAsync(
            trader,
            "/api/rfqs/confirm-quotes",
            new
            {
                Items = new[]
                {
                    new
                    { created.CaseId,
                        Expiry = new { Type = "None", Minutes = (int?)null },
                        ExpectedCurrentVersion = row.CurrentVersion,
                        ExpectedWorkingQuoteVersion = working.Version,
                    },
                },
            }));
        Assert.Equal("Applied", quote.Status);

        row = await GetTraderRowAsync(trader, created.CaseId);
        CaseOperationBody forbidden = Assert.Single(await PostOperationsAsync(
            trader,
            "/api/rfqs/present",
            new
            {
                Items = new[] { new { row.CaseId, ExpectedCurrentVersion = row.CurrentVersion } },
            }));
        Assert.Equal("Failed", forbidden.Status);
        Assert.Equal("Forbidden", forbidden.FailureCode);

        CaseOperationBody presented = Assert.Single(await PostOperationsAsync(
            sales,
            "/api/rfqs/present",
            new
            {
                Items = new[] { new { row.CaseId, ExpectedCurrentVersion = row.CurrentVersion } },
            }));
        Assert.Equal("Applied", presented.Status);
    }

    [Fact]
    public async Task Reference_search_settings_and_search_use_final_routes()
    {
        using HttpClient client = CreateClient("sales-dev");

        Assert.Single((await client.GetFromJsonAsync<List<SecurityBody>>(
            "/api/reference-data/securities?q=375-1"))!);
        Assert.Single((await client.GetFromJsonAsync<List<ClientBody>>(
            "/api/reference-data/clients?q=C001"))!);
        Assert.Equal(
            2,
            (await client.GetFromJsonAsync<List<UserBody>>(
                "/api/reference-data/assignable-traders"))!.Count);

        HttpResponseMessage theme = await client.PutAsJsonAsync(
            "/api/me/settings/theme", new { Mode = "Light" });
        theme.EnsureSuccessStatusCode();
        Assert.Equal("Light", (await theme.Content.ReadFromJsonAsync<ThemeBody>())!.Mode);

        HttpResponseMessage search = await client.GetAsync("/api/search/rfqs?caseId=1");
        search.EnsureSuccessStatusCode();
        Assert.Equal("application/json", search.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Development_identity_query_fallback_drives_final_trader_sse_endpoint()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        MeBody trader = (await client.GetFromJsonAsync<MeBody>(
            "/api/me?developmentUser=trader-a"))!;
        Assert.Equal("trader-a", trader.UserId);
        Assert.Contains("Trader", trader.Roles);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/worklists/stream?developmentUser=trader-a");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using HttpResponseMessage response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);
        string payload = string.Empty;
        while (!payload.Contains("data:", StringComparison.Ordinal))
        {
            payload += await reader.ReadLineAsync(timeout.Token) + "\n";
        }
        Assert.Contains("trader-list", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("sales-list", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Development_identity_header_wins_over_query_fallback()
    {
        using HttpClient client = CreateClient("sales-a");
        MeBody me = (await client.GetFromJsonAsync<MeBody>(
            "/api/me?developmentUser=trader-a"))!;

        Assert.Equal("sales-a", me.UserId);
        Assert.Contains("Sales", me.Roles);
        Assert.DoesNotContain("Trader", me.Roles);
    }

    private static async Task<InitialRfqBody> CreateDraftAsync(
        HttpClient client,
        string clientId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/rfqs/drafts", DraftRequest(clientId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InitialRfqBody>())!;
    }

    private static async Task<InitialRfqBody> ConfirmNewAsync(
        HttpClient client,
        string clientId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/rfqs/drafts/confirm", DraftRequest(clientId, 100_000_000m));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InitialRfqBody>())!;
    }

    private static object DraftRequest(string clientId, decimal? notional = null) => new
    {
        ClientId = clientId,
        SecurityId = "sec-jgb-375",
        Notional = notional,
        SettlementDate = "2026-09-24",
        StandardSettlementDate = "2026-09-23",
        SalesAndTradingMessage = "",
        AssignedTraderId = "trader-a",
    };

    private static async Task<IReadOnlyList<CaseOperationBody>> PostOperationsAsync(
        HttpClient client, string route, object request)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(route, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<CaseOperationBody>>())!;
    }

    private static async Task<TraderRfqBody> GetTraderRowAsync(
        HttpClient client, long caseId)
    {
        List<TraderRfqBody> rows = (await client.GetFromJsonAsync<List<TraderRfqBody>>(
            "/api/worklists/trader"))!;
        return Assert.Single(rows, item => item.CaseId == caseId);
    }

    private HttpClient CreateClient(string userId)
    {
        HttpClient client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Development-User", userId);
        return client;
    }

    private sealed record InitialRfqBody(
        long CaseId,
        long Version,
        decimal? Notional,
        string SalesAndTradingMessage,
        string AssignedTraderId);

    private sealed record SalesRfqBody(long CaseId, string RfqStatus, string? QuoteStatus);

    private sealed record TraderRfqBody(
        long CaseId,
        bool Owned,
        long CurrentVersion,
        long WorkingQuoteVersion);

    private sealed record WorkingQuoteBody(long Version);

    private sealed record CaseOperationBody(
        long CaseId,
        string Status,
        string? FailureCode,
        string? Message);

    private sealed record SecurityBody(string SecurityId);
    private sealed record ClientBody(string ClientId);
    private sealed record UserBody(string UserId);
    private sealed record ThemeBody(string Mode);
    private sealed record MeBody(string UserId, IReadOnlyList<string> Roles);

    private sealed record ApiProblemBody(
        string Code,
        string Detail,
        string TraceId,
        IReadOnlyDictionary<string, string[]> Errors);
}
