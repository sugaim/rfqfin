using Rfq.Domain;

namespace Rfq.Application;

public sealed class ScratchPricer(ICalculationClient calculationClient)
{
    public async Task<CalculatedQuotePayload> ExecuteAsync(ScratchPriceRequest input,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        CalculationParameter parameter = input.Driver switch
        {
            CalculationDriver.Price => new PriceCalculationParameter(input.Value),
            CalculationDriver.BbgYield => new BbgYieldCalculationParameter(input.Value),
            CalculationDriver.SimpleYield => new SimpleYieldCalculationParameter(input.Value),
            CalculationDriver.GSpread => new GSpreadCalculationParameter(input.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(input)),
        };
        var result = (await calculationClient.CalculateBulkAsync([
            new(id, input.SecurityId, input.SettlementDate, input.Driver, parameter, input.SimpleYieldSlide)
        ], cancellationToken)).Single();
        return result switch
        {
            CalculationSuccess success => success.Payload,
            CalculationError error => throw new CalculationFailureException(id, error.Code, error.Message),
            _ => throw new RfqInvariantException("Unknown calculation response."),
        };
    }
}

public sealed record ScratchPriceRequest(SecurityId SecurityId, DateOnly SettlementDate,
    CalculationDriver Driver, decimal Value, decimal SimpleYieldSlide);
