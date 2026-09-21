namespace Rfq.Domain;

public readonly record struct QuoteId
{
    public QuoteId(Guid value)
    {
        if (value == Guid.Empty) throw new DomainValidationException("QuoteId cannot be empty.");
        Value = value;
    }
    public Guid Value { get; }
    public static QuoteId New() => new(Guid.NewGuid());
}
