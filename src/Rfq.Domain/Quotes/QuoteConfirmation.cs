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
