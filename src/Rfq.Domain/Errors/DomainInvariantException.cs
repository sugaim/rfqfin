namespace Rfq.Domain;

public sealed class DomainInvariantException : RfqInvariantException
{
    public DomainInvariantException(string message) : base(message) { }

    public DomainInvariantException(string message, Exception innerException)
        : base(message, innerException) { }
}
