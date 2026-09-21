namespace Rfq.Domain;

public readonly record struct RevisionId
{
    public RevisionId(Guid value)
    {
        if (value == Guid.Empty) throw new DomainValidationException("RevisionId cannot be empty.");
        Value = value;
    }
    public Guid Value { get; }
    public static RevisionId New() => new(Guid.NewGuid());
}
