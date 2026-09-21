using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreQuoteExpirySettings(RfqDbContext dbContext) : IQuoteExpirySettings
{
    public async Task<QuoteExpiry> GetAsync(UserId userId,
        CancellationToken cancellationToken = default)
    {
        var minutes = await dbContext.MasterUsers.AsNoTracking()
            .Where(item => item.UserId == userId.Value)
            .Select(item => new { Exists = true, item.DefaultQuoteExpiryMinutes })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RfqInvariantException($"User '{userId.Value}' was not found.");
        try
        {
            return minutes.DefaultQuoteExpiryMinutes is null
                ? new QuoteExpiry.None()
                : new QuoteExpiry.After(
                    TimeSpan.FromMinutes(minutes.DefaultQuoteExpiryMinutes.Value));
        }
        catch (DomainValidationException exception)
        {
            throw new RfqInvariantException("Persisted quote expiry is invalid.", exception);
        }
    }

    public async Task<QuoteExpiry> SaveAsync(UserId userId, QuoteExpiry expiry,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.MasterUsers.SingleOrDefaultAsync(
            item => item.UserId == userId.Value, cancellationToken)
            ?? throw new RfqInvariantException($"User '{userId.Value}' was not found.");
        user.DefaultQuoteExpiryMinutes = expiry switch
        {
            QuoteExpiry.None => null,
            QuoteExpiry.After after when after.Duration.TotalMinutes <= int.MaxValue
                && after.Duration.TotalMinutes == Math.Truncate(after.Duration.TotalMinutes)
                => checked((int)after.Duration.TotalMinutes),
            QuoteExpiry.After => throw new RfqRequestValidationException(
                "Quote expiry must be a whole number of minutes."),
            _ => throw new DomainInvariantException("Unknown Quote Expiry policy."),
        };
        await dbContext.SaveChangesAsync(cancellationToken);
        return expiry;
    }
}
