namespace Rfq.Domain;

public sealed record CategoryId
{
    private CategoryId(string value) => Value = value;
    public string Value { get; }
    public static CategoryId Create(string value) => new(Normalize(value));

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new DomainValidationException("CategoryId is required.") : value.Trim();
}
