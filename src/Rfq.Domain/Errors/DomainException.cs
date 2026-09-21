namespace Rfq.Domain;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}

public sealed class DomainRuleViolationException(string message) : DomainException(message);

public sealed class DomainValidationException(string message) : DomainException(message);

public sealed class StateVersionMismatchException : DomainException
{
    public StateVersionMismatchException(string message) : base(message) { }
    public StateVersionMismatchException(string message, Exception innerException)
        : base(message, innerException) { }
}

public sealed class DomainInvariantException(string message) : DomainException(message);
