using Rfq.Domain;

namespace Rfq.Application;

public sealed record CalculationRequest(
    Guid RequestId,
    string SecurityId,
    DateOnly SettlementDate,
    CalculationDriver Driver,
    CalculationParameter Parameter,
    decimal SimpleYieldSlide);
