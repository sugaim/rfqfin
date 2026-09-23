namespace Rfq.Domain;

public enum RfqErrorKind
{
    Validation,
    InvalidState,
    VersionConflict,
    NotFound,
    Forbidden,
    CalculationFailure,
    ServiceUnavailable,
}
