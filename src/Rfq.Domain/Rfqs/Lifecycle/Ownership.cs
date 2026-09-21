namespace Rfq.Domain;

public abstract record Ownership;
public sealed record Owned : Ownership;
public sealed record Unowned : Ownership;
