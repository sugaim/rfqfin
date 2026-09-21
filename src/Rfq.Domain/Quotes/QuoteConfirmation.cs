namespace Rfq.Domain;

public sealed class QuoteConfirmation
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

public abstract class QuoteExpiry
{
    private QuoteExpiry() { }
    public sealed class None : QuoteExpiry;
    public sealed class After : QuoteExpiry
    {
        public After(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
                throw new DomainValidationException("Quote expiry duration must be positive.");
            Duration = duration;
        }
        public TimeSpan Duration { get; }
    }
}
