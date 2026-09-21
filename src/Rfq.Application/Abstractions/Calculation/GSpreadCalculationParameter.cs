using Rfq.Domain;

namespace Rfq.Application;

public sealed record GSpreadCalculationParameter(decimal Spread)
    : CalculationParameter(Spread);
