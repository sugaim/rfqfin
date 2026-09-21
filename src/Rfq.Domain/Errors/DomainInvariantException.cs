namespace Rfq.Domain;

public sealed class DomainInvariantException(string message) : DomainException(message);
