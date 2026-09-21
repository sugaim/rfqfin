using Rfq.Domain;

namespace Rfq.Application;

public sealed record ScratchPriceRequest(string SecurityId, DateOnly SettlementDate,
    CalculationDriver Driver, decimal Value, decimal SimpleYieldSlide);
