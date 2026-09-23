using Microsoft.AspNetCore.Mvc;
using Rfq.Infrastructure;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(BusinessDateRfqSnapshotCoordinator coordinator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(CreateResponse());

    [HttpGet("readiness")]
    public ActionResult<HealthResponse> GetReadiness()
    {
        HealthResponse response = CreateResponse();
        return response.ReadModelAvailable
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private HealthResponse CreateResponse()
    {
        RfqReadModelStatus readModel = coordinator.GetStatus();
        return new HealthResponse(
            "ok",
            readModel.Available,
            readModel.RuntimeBusinessDate,
            readModel.SnapshotBusinessDate,
            readModel.CurrentGeneration,
            readModel.PublishedGeneration,
            readModel.LastSuccessfulRefreshAt,
            readModel.LastFailure);
    }
}

public sealed record HealthResponse(
    string Status,
    bool ReadModelAvailable,
    DateOnly? RuntimeBusinessDate,
    DateOnly? SnapshotBusinessDate,
    long CurrentGeneration,
    long PublishedGeneration,
    DateTimeOffset? LastSuccessfulRefreshAt,
    string? LastFailure);
