using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class RfqCaseRepository(RfqDbContext dbContext) : IRfqCaseRepository
{
    public void Add(RfqCase rfqCase)
    {
        ArgumentNullException.ThrowIfNull(rfqCase);

        var revision = new RfqRevisionEntity
        {
            RevisionId = rfqCase.InitialRevision.RevisionId.Value,
            CaseId = rfqCase.CaseId.Value,
            Status = rfqCase.InitialRevision.Status,
            Version = rfqCase.InitialRevision.Version,
            CreatedAt = rfqCase.InitialRevision.CreatedAt,
            CreatedBy = rfqCase.InitialRevision.CreatedBy.Value,
        };

        var entity = new RfqCaseEntity
        {
            CaseId = rfqCase.CaseId.Value,
            ClientId = rfqCase.ClientId.Value,
            SecurityId = rfqCase.SecurityId.Value,
            CreatedAt = rfqCase.CreatedAt,
            CreatedBy = rfqCase.CreatedBy.Value,
            SalesId = rfqCase.SalesId.Value,
            Revisions = [revision],
            Current = new CaseCurrentEntity
            {
                CaseId = rfqCase.CaseId.Value,
                Lifecycle = RfqLifecycleKind.Draft,
                RfqStatus = rfqCase.Status,
                CurrentRevisionId = revision.RevisionId,
                CurrentRevision = revision,
                Version = 1,
            },
        };

        dbContext.RfqCases.Add(entity);
    }

    public async Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(
        UserId salesUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(salesUserId);

        return await dbContext.RfqCases
            .AsNoTracking()
            .Where(entity =>
                entity.SalesId == salesUserId.Value
                && entity.Current.Lifecycle == RfqLifecycleKind.Draft)
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenBy(entity => entity.CaseId)
            .Select(entity => new SalesRfqListItem(
                entity.CaseId,
                entity.ClientId,
                entity.SecurityId,
                entity.Current.RfqStatus.ToString(),
                entity.Current.CurrentRevisionId,
                entity.Current.CurrentRevision.Status.ToString(),
                entity.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
