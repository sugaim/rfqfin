using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Eod;

[ApiController]
[Route("api/eod")]
public sealed class EodController(IEodQueries queries) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<EodResponse>> Get(
        [FromQuery] DateOnly date,
        CancellationToken token) => [.. (await queries.GetEodAsync(date, token))
        .Select(value => new EodResponse(
            value.ContactOwnerId.Value,
            value.Open,
            value.Hit,
            value.Away))];
}

public sealed record EodResponse(string ContactOwnerId, int Open, int Hit, int Away);
