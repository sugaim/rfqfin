using Rfq.Domain;

namespace Rfq.Application;

public sealed record CalculationSuccess(
    Guid RequestId,
    CalculatedQuotePayload Payload) : CalculationResult(RequestId);
