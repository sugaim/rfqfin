using Rfq.Application;

namespace Rfq.Infrastructure;

public sealed class EfUnitOfWork(RfqDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
