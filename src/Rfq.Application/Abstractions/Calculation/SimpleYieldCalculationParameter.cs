using Rfq.Domain;

namespace Rfq.Application;

public sealed record SimpleYieldCalculationParameter(decimal Yield)
    : CalculationParameter(Yield);
