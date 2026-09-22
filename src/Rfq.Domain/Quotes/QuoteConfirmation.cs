namespace Rfq.Domain;

public sealed record QuoteConfirmation
{
    public QuoteConfirmation(UserId confirmedBy, DateTimeOffset confirmedAt, QuoteExpiry expiry)
    {
        ConfirmedBy = confirmedBy;
        ConfirmedAt = confirmedAt.ToUniversalTime();
        Expiry = expiry ?? throw new DomainValidationException("Quote expiry policy is required.");
    }

    public UserId ConfirmedBy { get; }
    public DateTimeOffset ConfirmedAt { get; }
    public QuoteExpiry Expiry { get; }
}

public abstract record QuoteExpiry
{
    private QuoteExpiry() { }
    public sealed record None : QuoteExpiry;

    public sealed record After : QuoteExpiry
    {
        public After(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                throw new DomainValidationException("Quote expiry duration must be positive.");
            }

            Duration = duration;
        }

        public TimeSpan Duration { get; }
    }
}
