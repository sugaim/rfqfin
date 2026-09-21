using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/rfq-defaults")]
public sealed class RfqDefaultsController(ResolveRfqDefaults resolveDefaults) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<RfqDefaultsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RfqDefaultsResult>> Get(
        [FromQuery] string securityId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await resolveDefaults.ExecuteAsync(
                securityId,
                cancellationToken));
        }
        catch (Exception exception) when (
            exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
        {
            ModelState.AddModelError("request", exception.Message);
            return ValidationProblem(ModelState);
        }
    }
}
