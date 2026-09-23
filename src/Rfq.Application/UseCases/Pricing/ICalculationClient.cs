using Rfq.Domain;

namespace Rfq.Application;

public interface ICalculationClient
{
    Task<IReadOnlyList<CalculationResult>> CalculateBulkAsync(
        IReadOnlyList<CalculationRequest> requests,
        CancellationToken cancellationToken = default);
}
