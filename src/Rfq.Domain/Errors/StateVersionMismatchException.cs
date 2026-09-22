namespace Rfq.Domain;

public sealed class StateVersionMismatchException : ExpectedRfqException
{
    public StateVersionMismatchException(string message)
        : base(RfqErrorKind.VersionConflict, message) { }

    public StateVersionMismatchException(string message, Exception innerException)
        : base(RfqErrorKind.VersionConflict, message, innerException) { }
}
