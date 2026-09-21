using Rfq.Domain;

namespace Rfq.Application;

public sealed class RfqRequestValidationException(string message)
    : ExpectedRfqException(RfqErrorKind.Validation, message);

public sealed class RfqNotFoundException(string message)
    : ExpectedRfqException(RfqErrorKind.NotFound, message);

public sealed class RfqForbiddenException(string message)
    : ExpectedRfqException(RfqErrorKind.Forbidden, message);
