using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreSalesRfqQueries(RfqDbContext dbContext) : ISalesRfqQueries
{
    public async Task<IReadOnlyList<SalesRfqListItem>> GetAsync(
        UserId salesUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(salesUserId);

        return await dbContext.RfqCases
            .AsNoTracking()
            .Where(entity =>
                (entity.SalesId == salesUserId.Value
                    || entity.Current.ContactOwnerId == salesUserId.Value)
                && entity.Current.CurrentRevision.Status != RevisionStatus.Discarded
                && (entity.Current.Lifecycle == RfqLifecycleKind.Draft
                    || entity.Current.Lifecycle == RfqLifecycleKind.Open
                    || entity.Current.Lifecycle == RfqLifecycleKind.Cancelled
                    || entity.Current.Lifecycle == RfqLifecycleKind.Closed))
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenBy(entity => entity.CaseId)
            .Select(entity => new SalesRfqListItem(
                new CaseId(entity.CaseId),
                ClientId.Create(entity.ClientId),
                dbContext.Clients.Where(client => client.ClientId == entity.ClientId)
                    .Select(client => client.Name).FirstOrDefault() ?? entity.ClientId,
                SecurityId.Create(entity.SecurityId),
                dbContext.Securities.Where(security => security.SecurityId == entity.SecurityId)
                    .Select(security => security.JapaneseName).FirstOrDefault() ?? entity.SecurityId,
                dbContext.Securities.Where(security => security.SecurityId == entity.SecurityId)
                    .Select(security => security.BbgDisplay).FirstOrDefault() ?? entity.SecurityId,
                CategoryId.Create(entity.CategorySnapshot),
                entity.Current.RfqStatus,
                entity.Current.QuoteStatus,
                entity.Current.QuoteRequestReason,
                new RevisionId(entity.Current.CurrentRevisionId),
                entity.Current.CurrentQuoteId == null ? null : new QuoteId(entity.Current.CurrentQuoteId.Value),
                entity.Current.ClosedQuoteId == null ? null : new QuoteId(entity.Current.ClosedQuoteId.Value),
                new StateVersion(entity.Current.Version),
                entity.Current.CurrentRevision.Status,
                UserId.Create(entity.Current.ContactOwnerId),
                UserId.Create(entity.Current.AssignedTraderId),
                entity.Current.CurrentRevision.SettlementDate,
                entity.Current.CurrentRevision.StandardSettlementDate,
                entity.Current.CurrentRevision.Notional,
                entity.Current.CurrentRevision.SalesAndTradingMessage,
                entity.SalesMemo.Value,
                new StateVersion(entity.SalesMemo.Version),
                new StateVersion(entity.Current.CurrentRevision.Version),
                entity.CreatedAt,
                ToRevisionId(entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => (Guid?)revision.RevisionId).SingleOrDefault()),
                ToStateVersion(entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => (long?)revision.Version).SingleOrDefault()),
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => revision.SettlementDate).SingleOrDefault(),
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => revision.Notional).SingleOrDefault(),
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => revision.SalesAndTradingMessage).SingleOrDefault()))
            .ToListAsync(cancellationToken);
    }

    private static RevisionId? ToRevisionId(Guid? value) =>
        value is null ? null : new RevisionId(value.Value);

    private static StateVersion? ToStateVersion(long? value) =>
        value is null ? null : new StateVersion(value.Value);
}
