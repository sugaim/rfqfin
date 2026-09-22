namespace Rfq.Domain;

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
