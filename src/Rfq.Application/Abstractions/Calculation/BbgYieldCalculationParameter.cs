using Rfq.Domain;

namespace Rfq.Application;

public sealed record BbgYieldCalculationParameter(decimal Yield)
    : CalculationParameter(Yield);
