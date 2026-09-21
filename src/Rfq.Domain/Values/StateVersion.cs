namespace Rfq.Domain;

public readonly record struct StateVersion
{
    public StateVersion(long value)
    {
        if (value < 1)
        {
            throw new DomainValidationException("StateVersion must be at least 1.");
        }

        Value = value;
    }

    public long Value { get; }

    public StateVersion Next()
    {
        try
        {
            return new StateVersion(checked(Value + 1));
        }
        catch (OverflowException exception)
        {
            throw new DomainInvariantException(
                $"StateVersion {Value} cannot be incremented.", exception);
        }
    }

    public override string ToString() => Value.ToString();
}
