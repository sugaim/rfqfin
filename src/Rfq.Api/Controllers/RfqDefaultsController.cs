using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/rfq-defaults")]
public sealed class RfqDefaultsController(ResolveRfqDefaults resolveDefaults) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<RfqDefaultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RfqDefaultsResponse>> Get(
        [FromQuery] string securityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await resolveDefaults.ExecuteAsync(
                SecurityId.Create(securityId),
                cancellationToken);
            return Ok(new RfqDefaultsResponse(
                result.SecurityId.Value,
                result.CategoryId.Value,
                result.CategoryName,
                result.ContactOwnerId.Value,
                result.ContactOwnerName,
                result.AssignedTraderId.Value,
                result.AssignedTraderName,
                result.SystemDate,
                result.StandardSettlementDate));
        }
        catch (Exception exception) when (
            exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
        {
            ModelState.AddModelError("request", exception.Message);
            return ValidationProblem(ModelState);
        }
    }
}

public sealed record RfqDefaultsResponse(
    string SecurityId,
    string CategoryId,
    string CategoryName,
    string ContactOwnerId,
    string ContactOwnerName,
    string AssignedTraderId,
    string AssignedTraderName,
    DateOnly SystemDate,
    DateOnly StandardSettlementDate);
