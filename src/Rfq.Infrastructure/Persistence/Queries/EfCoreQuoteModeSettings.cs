using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreQuoteModeSettings(RfqDbContext dbContext) : IQuoteModeSettings
{
    public async Task<WorkingQuoteMode> GetAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var mode = await dbContext.MasterUsers.AsNoTracking()
            .Where(item => item.UserId == userId.Value)
            .Select(item => new { item.DefaultQuoteMode })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RfqInvariantException($"User '{userId}' was not found.");
        return mode.DefaultQuoteMode ?? WorkingQuoteMode.Calculated;
    }

    public async Task<WorkingQuoteMode> SaveAsync(
        UserId userId,
        WorkingQuoteMode mode,
        CancellationToken cancellationToken = default)
    {
        MasterUserEntity user = await dbContext.MasterUsers.SingleOrDefaultAsync(
            item => item.UserId == userId.Value, cancellationToken)
            ?? throw new RfqInvariantException($"User '{userId}' was not found.");
        user.DefaultQuoteMode = mode;
        await dbContext.SaveChangesAsync(cancellationToken);
        return mode;
    }
}
