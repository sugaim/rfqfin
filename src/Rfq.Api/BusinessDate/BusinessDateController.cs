using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.BusinessDate;

[ApiController]
[Route("api/business-date")]
public sealed class BusinessDateController(IBusinessDateProvider businessDate) : ControllerBase
{
    [HttpGet]
    public async Task<BusinessDateResponse> Get(CancellationToken cancellationToken) =>
        new(await businessDate.GetCurrentAsync(cancellationToken));
}

public sealed record BusinessDateResponse(DateOnly Date);
