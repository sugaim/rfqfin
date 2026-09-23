using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqCreationContext;

[ApiController]
[Route("api/rfqs/creation-context")]
public sealed class RfqCreationContextController(
    ResolveRfqCreationContext resolver) : ControllerBase
{
    [HttpGet]
    [EndpointName("ResolveRfqCreationContext")]
    public async Task<RfqCreationContextResponse> Get(
        [FromQuery, Required, MinLength(1)] string securityId,
        CancellationToken cancellationToken)
    {
        Application.RfqCreationContext value = await resolver.ExecuteAsync(SecurityId.Create(securityId), cancellationToken);
        return RfqCreationContextApiMapper.ToApi(value);
    }
}

public sealed record RfqCreationContextResponse(
    string CategoryId,
    string CategoryName,
    string DefaultAssignedTraderId,
    string DefaultAssignedTraderName,
    DateOnly StandardSettlementDate);

public static class RfqCreationContextApiMapper
{
    public static RfqCreationContextResponse ToApi(Application.RfqCreationContext value) => new(
        value.CategoryId.Value,
        value.CategoryName,
        value.DefaultAssignedTraderId.Value,
        value.DefaultAssignedTraderName,
        value.StandardSettlementDate);
}
