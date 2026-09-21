using Rfq.Domain;

namespace Rfq.Application;

public sealed class CalculationFailureException(
    Guid failureLogId,
    string code,
    string message) : Exception(message)
{
    public Guid FailureLogId { get; } = failureLogId;

    public string Code { get; } = code;
}
