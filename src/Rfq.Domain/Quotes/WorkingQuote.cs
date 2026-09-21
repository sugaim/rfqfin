namespace Rfq.Domain;

public sealed class WorkingQuote
{
    internal WorkingQuote(
        RevisionId revisionId, WorkingQuoteMode mode,
        CalculatedQuotePayload? calculated, ManualQuotePayload? manual,
        StateVersion version, DateTimeOffset createdAt, UserId createdBy,
        DateTimeOffset updatedAt, UserId updatedBy)
    {
        RevisionId = revisionId;
        Mode = mode;
        Calculated = calculated;
        Manual = manual;
        Version = version;
        CreatedAt = createdAt.ToUniversalTime();
        CreatedBy = createdBy;
        UpdatedAt = updatedAt.ToUniversalTime();
        UpdatedBy = updatedBy;
    }

    public RevisionId RevisionId { get; }
    public WorkingQuoteMode Mode { get; }
    public CalculatedQuotePayload? Calculated { get; }
    public ManualQuotePayload? Manual { get; }
    public StateVersion Version { get; }
    public DateTimeOffset CreatedAt { get; }
    public UserId CreatedBy { get; }
    public DateTimeOffset UpdatedAt { get; }
    public UserId UpdatedBy { get; }

    internal static WorkingQuote Restore(
        RevisionId revisionId, WorkingQuoteMode mode,
        CalculatedQuotePayload? calculated, ManualQuotePayload? manual,
        StateVersion version, DateTimeOffset createdAt, UserId createdBy,
        DateTimeOffset updatedAt, UserId updatedBy) => new(
            revisionId, mode, calculated, manual, version,
            createdAt, createdBy, updatedAt, updatedBy);
}

public enum WorkingQuoteMode { Calculated, Manual }

public sealed record CalculatedQuotePayload(
    CalculationDriver Driver, decimal DriverValue, decimal Price,
    decimal BbgYield, decimal BaseSimpleYield, decimal SimpleYieldSlide,
    decimal FinalSimpleYield, decimal InternalYield, decimal GSpread, decimal Asw);

public sealed record ManualQuotePayload(decimal? Price, decimal? FinalSimpleYield);

public enum CalculationDriver { Price, BbgYield, SimpleYield, GSpread }
