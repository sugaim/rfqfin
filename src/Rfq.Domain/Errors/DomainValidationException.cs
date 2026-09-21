namespace Rfq.Domain;

public sealed class DomainValidationException(string message)
    : ExpectedRfqException(RfqErrorKind.Validation, message);
