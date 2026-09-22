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

public sealed record CalculatedQuotePayload
{
    public CalculatedQuotePayload(
        CalculationDriver driver,
        decimal driverValue,
        decimal price,
        decimal bbgYield,
        decimal baseSimpleYield,
        decimal simpleYieldSlide,
        decimal finalSimpleYield,
        decimal internalYield,
        decimal gSpread,
        decimal asw,
        decimal ysc = 0m,
        decimal iSpread = 0m,
        decimal zSpread = 0m)
    {
        Driver = driver;
        DriverValue = driverValue;
        Price = price;
        BbgYield = bbgYield;
        BaseSimpleYield = baseSimpleYield;
        SimpleYieldSlide = simpleYieldSlide;
        FinalSimpleYield = finalSimpleYield;
        InternalYield = internalYield;
        GSpread = gSpread;
        Asw = asw;
        Ysc = ysc;
        ISpread = iSpread;
        ZSpread = zSpread;
    }

    public CalculationDriver Driver { get; }
    public decimal DriverValue { get; }
    public decimal Price { get; }
    public decimal BbgYield { get; }
    public decimal BaseSimpleYield { get; }
    public decimal SimpleYieldSlide { get; }
    public decimal FinalSimpleYield { get; }
    public decimal InternalYield { get; }
    public decimal GSpread { get; }
    public decimal Asw { get; }
    public decimal Ysc { get; }
    public decimal ISpread { get; }
    public decimal ZSpread { get; }
}

public sealed record ManualQuotePayload
{
    public ManualQuotePayload(decimal? price, decimal? finalSimpleYield)
    {
        Price = price;
        FinalSimpleYield = finalSimpleYield;
    }

    public decimal? Price { get; }
    public decimal? FinalSimpleYield { get; }
}

public enum CalculationDriver
{
    Price,
    BbgYield,
    SimpleYield,
    Ysc,
    GSpread,
    Asw,
    ISpread,
    ZSpread,
}
