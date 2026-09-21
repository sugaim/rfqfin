using Rfq.Domain;

namespace Rfq.Application;

public sealed record CalculationError(
    Guid RequestId,
    string Code,
    string Message) : CalculationResult(RequestId);
