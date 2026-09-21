namespace Rfq.Domain;

public abstract class RfqException : Exception
{
    protected RfqException(string message) : base(message) { }

    protected RfqException(string message, Exception innerException)
        : base(message, innerException) { }
}

public abstract class ExpectedRfqException : RfqException
{
    protected ExpectedRfqException(RfqErrorKind kind, string message)
        : base(message) => Kind = kind;

    protected ExpectedRfqException(
        RfqErrorKind kind,
        string message,
        Exception innerException)
        : base(message, innerException) => Kind = kind;

    public RfqErrorKind Kind { get; }
}

public class RfqInvariantException : RfqException
{
    public RfqInvariantException(string message) : base(message) { }

    public RfqInvariantException(string message, Exception innerException)
        : base(message, innerException) { }
}
