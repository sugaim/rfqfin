namespace Rfq.Domain;

public readonly record struct CaseId
{
    public CaseId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "CaseId must be a positive integer.");
        }

        Value = value;
    }

    public long Value { get; }
}

public readonly record struct RevisionId(Guid Value)
{
    public static RevisionId New() => new(Guid.NewGuid());
}

public sealed record ClientId
{
    private ClientId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ClientId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new ClientId(value.Trim());
    }
}

public sealed record SecurityId
{
    private SecurityId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SecurityId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new SecurityId(value.Trim());
    }
}

public sealed record CategoryId
{
    private CategoryId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CategoryId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new CategoryId(value.Trim());
    }
}

public sealed record UserId
{
    private UserId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static UserId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new UserId(value.Trim());
    }
}
