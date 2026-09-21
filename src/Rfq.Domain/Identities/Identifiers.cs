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

public sealed record ClientId
{
    private ClientId(string value) => Value = value;
    public string Value { get; }
    public static ClientId Create(string value) => new(Normalize(value, nameof(ClientId)));
    private static string Normalize(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new DomainValidationException($"{name} is required.")
            : value.Trim();
}

public sealed record SecurityId
{
    private SecurityId(string value) => Value = value;
    public string Value { get; }
    public static SecurityId Create(string value) => new(Normalize(value));
    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new DomainValidationException("SecurityId is required.") : value.Trim();
}

public sealed record CategoryId
{
    private CategoryId(string value) => Value = value;
    public string Value { get; }
    public static CategoryId Create(string value) => new(Normalize(value));
    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new DomainValidationException("CategoryId is required.") : value.Trim();
}

public sealed record UserId
{
    private UserId(string value) => Value = value;
    public string Value { get; }
    public static UserId Create(string value) => new(Normalize(value));
    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new DomainValidationException("UserId is required.") : value.Trim();
}
