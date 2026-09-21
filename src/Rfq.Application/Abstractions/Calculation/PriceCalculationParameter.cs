using Rfq.Domain;

namespace Rfq.Application;

public sealed record PriceCalculationParameter(decimal Price)
    : CalculationParameter(Price);
