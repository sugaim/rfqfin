using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/system-date")]
public sealed class SystemDateController(ISystemDateProvider systemDateProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SystemDateResponse>> Get(CancellationToken cancellationToken)
    {
        var date = await systemDateProvider.GetTodayAsync(cancellationToken);
        return Ok(new SystemDateResponse(date));
    }
}

public sealed record SystemDateResponse(DateOnly Date);
