namespace Rfq.Domain;

public enum WorkingQuoteMode
{
    Calculated,
    Manual,
}

public enum CalculationDriver
{
    Price,
    BbgYield,
    SimpleYield,
    GSpread,
}

public sealed record CalculatedQuotePayload(
    CalculationDriver Driver,
    decimal DriverValue,
    decimal Price,
    decimal BbgYield,
    decimal BaseSimpleYield,
    decimal SimpleYieldSlide,
    decimal FinalSimpleYield,
    decimal InternalYield,
    decimal GSpread,
    decimal Asw);

public sealed record ManualQuotePayload(
    decimal? Price,
    decimal? FinalSimpleYield);

public sealed class WorkingQuote
{
    private WorkingQuote(
        RevisionId revisionId,
        WorkingQuoteMode mode,
        CalculatedQuotePayload? calculated,
        ManualQuotePayload? manual,
        long version,
        DateTimeOffset createdAt,
        UserId createdBy,
        DateTimeOffset updatedAt,
        UserId updatedBy)
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

    public WorkingQuoteMode Mode { get; private set; }

    public CalculatedQuotePayload? Calculated { get; private set; }

    public ManualQuotePayload? Manual { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public UserId CreatedBy { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public UserId UpdatedBy { get; private set; }

    public static WorkingQuote CreateEmpty(
        RevisionId revisionId,
        UserId createdBy,
        DateTimeOffset createdAt) => new(
        revisionId,
        WorkingQuoteMode.Calculated,
        null,
        null,
        1,
        createdAt,
        createdBy,
        createdAt,
        createdBy);

    public static WorkingQuote Clone(
        RevisionId revisionId,
        WorkingQuote seed,
        UserId createdBy,
        DateTimeOffset createdAt) => new(
        revisionId,
        seed.Mode,
        seed.Calculated,
        seed.Manual,
        1,
        createdAt,
        createdBy,
        createdAt,
        createdBy);

    public static WorkingQuote Restore(
        RevisionId revisionId,
        WorkingQuoteMode mode,
        CalculatedQuotePayload? calculated,
        ManualQuotePayload? manual,
        long version,
        DateTimeOffset createdAt,
        UserId createdBy,
        DateTimeOffset updatedAt,
        UserId updatedBy) => new(
        revisionId,
        mode,
        calculated,
        manual,
        version,
        createdAt,
        createdBy,
        updatedAt,
        updatedBy);

    public void ApplyCalculated(
        CalculatedQuotePayload payload,
        long expectedVersion,
        UserId updatedBy,
        DateTimeOffset updatedAt)
    {
        EnsureVersion(expectedVersion);
        Calculated = payload;
        Mode = WorkingQuoteMode.Calculated;
        Touch(updatedBy, updatedAt);
    }

    public void SwitchMode(
        WorkingQuoteMode mode,
        long expectedVersion,
        UserId updatedBy,
        DateTimeOffset updatedAt)
    {
        EnsureVersion(expectedVersion);
        if (mode == WorkingQuoteMode.Manual && Mode != WorkingQuoteMode.Manual)
        {
            Manual = new ManualQuotePayload(null, null);
        }

        Mode = mode;
        Touch(updatedBy, updatedAt);
    }

    public void UpdateManual(
        decimal? price,
        decimal? finalSimpleYield,
        long expectedVersion,
        UserId updatedBy,
        DateTimeOffset updatedAt)
    {
        EnsureVersion(expectedVersion);
        if (Mode != WorkingQuoteMode.Manual)
        {
            throw new InvalidOperationException("Manual values can only be edited in Manual mode.");
        }

        Manual = new ManualQuotePayload(price, finalSimpleYield);
        Touch(updatedBy, updatedAt);
    }

    private void EnsureVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException("The WorkingQuote was changed by another user.");
        }
    }

    private void Touch(UserId updatedBy, DateTimeOffset updatedAt)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt.ToUniversalTime();
        Version++;
    }
}
