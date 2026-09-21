namespace Rfq.Domain;

public sealed class DomainRuleViolationException(string message) : DomainException(message);
