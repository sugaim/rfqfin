namespace Rfq.Domain;

public sealed class DomainValidationException(string message) : DomainException(message);
