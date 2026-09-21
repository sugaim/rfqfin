namespace Rfq.Domain;

public sealed class DomainRuleViolationException(string message)
    : ExpectedRfqException(RfqErrorKind.InvalidState, message);
