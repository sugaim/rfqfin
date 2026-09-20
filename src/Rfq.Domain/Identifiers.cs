namespace Rfq.Domain;

public readonly record struct CaseId(Guid Value)
{
    public static CaseId New() => new(Guid.NewGuid());
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
