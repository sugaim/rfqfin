namespace Rfq.Domain;

public sealed record CalculatedQuotePayload(
    CalculationDriver Driver, decimal DriverValue, decimal Price,
    decimal BbgYield, decimal BaseSimpleYield, decimal SimpleYieldSlide,
    decimal FinalSimpleYield, decimal InternalYield, decimal GSpread, decimal Asw);
