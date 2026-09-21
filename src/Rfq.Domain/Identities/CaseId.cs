namespace Rfq.Domain;

public readonly record struct CaseId
{
    public CaseId(long value)
    {
        if (value <= 0) throw new DomainValidationException("CaseId must be positive.");
        Value = value;
    }
    public long Value { get; }
}
