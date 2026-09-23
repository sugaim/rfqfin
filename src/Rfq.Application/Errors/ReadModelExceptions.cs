using Rfq.Domain;

namespace Rfq.Application;

public sealed class RfqReadModelUnavailableException(string message, Exception? innerException = null)
    : ExpectedRfqException(RfqErrorKind.ServiceUnavailable, message, innerException ?? new InvalidOperationException(message));

public sealed class BusinessDateChangedException(DateOnly authoritativeBusinessDate)
    : Exception("The authoritative Business Date changed while the worklist was loading.")
{
    public DateOnly AuthoritativeBusinessDate { get; } = authoritativeBusinessDate;
}
