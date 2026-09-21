namespace Rfq.Domain;

public sealed record DeskId
{
    private DeskId(string value) => Value = value;

    public string Value { get; }

    public static DeskId Create(string value) => new(Normalize(value));

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new DomainValidationException("DeskId is required.")
        : value.Trim();

    public override string ToString() => Value;
}
