using Rfq.Domain;

namespace Rfq.Application;

public sealed record CalculationRequest(
    Guid RequestId,
    SecurityId SecurityId,
    DateOnly SettlementDate,
    CalculationDriver Driver,
    CalculationParameter Parameter,
    decimal SimpleYieldSlide);

public abstract record CalculationResult(Guid RequestId);

public sealed record CalculationSuccess(
    Guid RequestId,
    CalculatedQuotePayload Payload) : CalculationResult(RequestId);

public sealed record CalculationError(
    Guid RequestId,
    string Code,
    string Message) : CalculationResult(RequestId);

public abstract record CalculationParameter(decimal Value);

public sealed record PriceCalculationParameter(decimal Price)
    : CalculationParameter(Price);

public sealed record BbgYieldCalculationParameter(decimal Yield)
    : CalculationParameter(Yield);

public sealed record SimpleYieldCalculationParameter(decimal Yield)
    : CalculationParameter(Yield);

public sealed record GSpreadCalculationParameter(decimal Spread)
    : CalculationParameter(Spread);

public sealed record YscCalculationParameter(decimal Spread)
    : CalculationParameter(Spread);

public sealed record AswCalculationParameter(decimal Spread)
    : CalculationParameter(Spread);

public sealed record ISpreadCalculationParameter(decimal Spread)
    : CalculationParameter(Spread);

public sealed record ZSpreadCalculationParameter(decimal Spread)
    : CalculationParameter(Spread);
