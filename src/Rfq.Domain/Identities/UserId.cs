namespace Rfq.Domain;

public sealed record UserId
{
    private UserId(string value) => Value = value;
    public string Value { get; }
    public static UserId Create(string value) => new(Normalize(value));

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new DomainValidationException("UserId is required.") : value.Trim();
}
