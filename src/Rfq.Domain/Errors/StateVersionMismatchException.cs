namespace Rfq.Domain;

public sealed class StateVersionMismatchException : DomainException
{
    public StateVersionMismatchException(string message) : base(message) { }
    public StateVersionMismatchException(string message, Exception innerException)
        : base(message, innerException) { }
}
