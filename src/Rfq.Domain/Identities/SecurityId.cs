namespace Rfq.Domain;

public sealed record SecurityId
{
    private SecurityId(string value) => Value = value;
    public string Value { get; }
    public static SecurityId Create(string value) => new(Normalize(value));
    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new DomainValidationException("SecurityId is required.") : value.Trim();
}
