using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;


public sealed class MockCalculationClient : ICalculationClient
{
    public Task<IReadOnlyList<CalculationResult>> CalculateBulkAsync(
        IReadOnlyList<CalculationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CalculationResult> results = requests
            .Select(Calculate)
            .ToArray();
        return Task.FromResult(results);
    }

    private static CalculationResult Calculate(CalculationRequest request)
    {
        if (request.Parameter.Value == -999m)
        {
            return new CalculationError(
                request.RequestId,
                "MOCK_FORCED_FAILURE",
                "Mock calculation failure was requested.");
        }

        var securityBasis = request.SecurityId.Value.Sum(character => character) % 50 / 100m;
        var referenceYield = 0.7m + securityBasis;
        var (price, baseSimpleYield, gSpread) = request.Driver switch
        {
            CalculationDriver.Price => (
                request.Parameter.Value,
                referenceYield + (100m - request.Parameter.Value) / 20m,
                (referenceYield + (100m - request.Parameter.Value) / 20m - referenceYield) * 100m),
            CalculationDriver.BbgYield => (
                100m - (request.Parameter.Value - referenceYield) * 20m,
                request.Parameter.Value + 0.015m,
                (request.Parameter.Value - referenceYield) * 100m),
            CalculationDriver.SimpleYield => (
                100m - (request.Parameter.Value - referenceYield) * 20m,
                request.Parameter.Value,
                (request.Parameter.Value - referenceYield) * 100m),
            CalculationDriver.GSpread => (
                100m - request.Parameter.Value / 5m,
                referenceYield + request.Parameter.Value / 100m,
                request.Parameter.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
        var payload = new CalculatedQuotePayload(
            request.Driver,
            Round(request.Parameter.Value),
            Round(price),
            Round(baseSimpleYield - 0.01m),
            Round(baseSimpleYield),
            Round(request.SimpleYieldSlide),
            Round(baseSimpleYield + request.SimpleYieldSlide),
            Round(baseSimpleYield + 0.02m),
            Round(gSpread),
            Round(gSpread + 3m));
        return new CalculationSuccess(request.RequestId, payload);
    }

    private static decimal Round(decimal value) => Math.Round(value, 6);
}
