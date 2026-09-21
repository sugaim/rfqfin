using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/operations")]
public sealed class OperationsController(
    IOperationalQueries queries,
    ScratchPricer pricer) : ControllerBase
{
    [HttpGet("past-rfqs")]
    public Task<PastRfqResult> Search([FromQuery] PastRfqSearch search,
        CancellationToken cancellationToken) => queries.SearchAsync(search, cancellationToken);

    [HttpGet("rfqs/{caseId:long}/revisions")]
    public Task<IReadOnlyList<RevisionHistoryItem>> Revisions(long caseId,
        CancellationToken cancellationToken) => queries.GetRevisionHistoryAsync(caseId, cancellationToken);

    [HttpGet("rfqs/{caseId:long}/quotes")]
    public Task<IReadOnlyList<QuoteHistoryItem>> Quotes(long caseId,
        CancellationToken cancellationToken) => queries.GetQuoteHistoryAsync(caseId, cancellationToken);

    [HttpGet("eod")]
    public Task<IReadOnlyList<EodSummaryItem>> Eod([FromQuery] DateOnly date,
        CancellationToken cancellationToken) => queries.GetEodAsync(date, cancellationToken);

    [HttpGet("grid-config/{screenId}/{configKey}")]
    public async Task<ActionResult<GridConfig>> GetGridConfig(string screenId, string configKey,
        CancellationToken cancellationToken)
    {
        var value = await queries.GetGridConfigAsync(screenId, configKey, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPut("grid-config/{screenId}/{configKey}")]
    public Task<GridConfig> SaveGridConfig(string screenId, string configKey,
        GridConfigRequest request, CancellationToken cancellationToken) =>
        queries.SaveGridConfigAsync(screenId, configKey, request.Version,
            request.ConfigJson, cancellationToken);

    [HttpPost("pricer")]
    public Task<CalculatedQuotePayload> Price(ScratchPriceRequest request,
        CancellationToken cancellationToken) => pricer.ExecuteAsync(request, cancellationToken);
}

public sealed record GridConfigRequest(int Version, string ConfigJson);
