namespace Rfq.Domain;

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

    public static QuoteExpiry FromMinutes(int? minutes) => minutes is null
        ? new None()
        : new After(TimeSpan.FromMinutes(minutes.Value));
}
