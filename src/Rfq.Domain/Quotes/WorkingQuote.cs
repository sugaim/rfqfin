namespace Rfq.Domain;

public enum WorkingQuoteMode { Calculated, Manual }
public enum CalculationDriver { Price, BbgYield, SimpleYield, GSpread }

public sealed record CalculatedQuotePayload(
    CalculationDriver Driver, decimal DriverValue, decimal Price,
    decimal BbgYield, decimal BaseSimpleYield, decimal SimpleYieldSlide,
    decimal FinalSimpleYield, decimal InternalYield, decimal GSpread, decimal Asw);

public sealed record ManualQuotePayload(decimal? Price, decimal? FinalSimpleYield);

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

public static class WorkingQuoteFactory
{
    public static WorkingQuote CreateInitialFor(
        RfqCase rfq, UserId createdBy, DateTimeOffset createdAt)
    {
        if (rfq.Lifecycle is not OpenRfq
            || rfq.CurrentRevision.Status != RevisionStatus.Confirmed)
            throw new DomainRuleViolationException(
                "A WorkingQuote requires an Open RFQ with a confirmed current Revision.");
        return Empty(rfq.CurrentRevision.RevisionId, createdBy, createdAt);
    }

    public static WorkingQuote CreateForAmendment(
        RfqCase rfq, WorkingQuote? seed, UserId createdBy, DateTimeOffset createdAt)
    {
        if (rfq.Lifecycle is not ActiveRfq { QuoteState: QuoteRequested { Reason: QuoteRequestReason.Revised } })
            throw new DomainRuleViolationException(
                "An amendment WorkingQuote requires an Active Revised quote request.");
        return seed is null
            ? Empty(rfq.CurrentRevision.RevisionId, createdBy, createdAt)
            : new WorkingQuote(rfq.CurrentRevision.RevisionId, seed.Mode, seed.Calculated,
                seed.Manual, new StateVersion(1), createdAt, createdBy, createdAt, createdBy);
    }

    private static WorkingQuote Empty(RevisionId revisionId, UserId by, DateTimeOffset at) =>
        new(revisionId, WorkingQuoteMode.Calculated, null, null,
            new StateVersion(1), at, by, at, by);
}
