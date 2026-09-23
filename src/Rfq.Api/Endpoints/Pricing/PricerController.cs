using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Api.RfqQuotes;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.Pricer;

[ApiController]
[Route("api/pricing/scratch")]
public sealed class PricerController(ScratchPrice pricer) : ControllerBase
{
    [HttpPost]
    [EndpointName("ScratchPrice")]
    public async Task<CalculatedQuoteResponse> Price(
        PricerRequest request,
        CancellationToken token) => QuoteApiMapper.ToApi(await pricer.ExecuteAsync(
            new ScratchPriceRequest(
                SecurityId.Create(request.SecurityId),
                request.SettlementDate,
                QuoteApiMapper.ToDomain(request.Driver),
                request.Value,
                request.SimpleYieldSlide),
            token))!;
}

public sealed record PricerRequest(
    [Required, MinLength(1)] string SecurityId,
    DateOnly SettlementDate,
    [Required] CalculationDriverValue Driver,
    decimal Value,
    decimal SimpleYieldSlide);
