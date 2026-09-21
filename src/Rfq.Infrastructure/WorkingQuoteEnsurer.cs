using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class WorkingQuoteEnsurer(RfqDbContext dbContext) : IWorkingQuoteEnsurer
{
    public async Task EnsureAsync(
        RevisionId revisionId,
        UserId createdBy,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        var revisionValue = revisionId.Value;
        if (dbContext.WorkingQuotes.Local.Any(item => item.RevisionId == revisionValue)
            || await dbContext.WorkingQuotes.AnyAsync(
                item => item.RevisionId == revisionValue,
                cancellationToken))
        {
            return;
        }

        dbContext.WorkingQuotes.Add(new WorkingQuoteEntity
        {
            RevisionId = revisionValue,
            Version = 1,
            CreatedAt = createdAt.ToUniversalTime(),
            CreatedBy = createdBy.Value,
        });
    }
}
