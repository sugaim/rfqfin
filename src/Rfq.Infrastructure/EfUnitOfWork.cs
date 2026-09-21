using Microsoft.EntityFrameworkCore;
using Rfq.Application;

namespace Rfq.Infrastructure;

public sealed class EfUnitOfWork(RfqDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException(
                "The RFQ was changed by another user. Reload and try again.",
                exception);
        }
    }
}
