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
            SettlementDate = rfqCase.InitialRevision.SettlementDate,
            StandardSettlementDate = rfqCase.InitialRevision.StandardSettlementDate,
            Notional = rfqCase.InitialRevision.Notional,
            SalesAndTradingMessage = rfqCase.InitialRevision.SalesAndTradingMessage,
            ConfirmedAt = rfqCase.InitialRevision.ConfirmedAt,
            ConfirmedBy = rfqCase.InitialRevision.ConfirmedBy?.Value,
        };

        var entity = new RfqCaseEntity
        {
            CaseId = rfqCase.CaseId.Value,
            ClientId = rfqCase.ClientId.Value,
            SecurityId = rfqCase.SecurityId.Value,
            CategorySnapshot = rfqCase.CategorySnapshot.Value,
            CreatedAt = rfqCase.CreatedAt,
            CreatedBy = rfqCase.CreatedBy.Value,
            SalesId = rfqCase.SalesId.Value,
            Revisions = [revision],
            Current = new CaseCurrentEntity
            {
                CaseId = rfqCase.CaseId.Value,
                Lifecycle = rfqCase.Lifecycle is OpenRfq
                    ? RfqLifecycleKind.Open
                    : RfqLifecycleKind.Draft,
                RfqStatus = rfqCase.Status,
                QuoteStatus = rfqCase.QuoteStatus,
                QuoteRequestReason = rfqCase.QuoteRequestReason,
                CurrentRevisionId = revision.RevisionId,
                CurrentRevision = revision,
                Version = 1,
                ContactOwnerId = rfqCase.ContactOwnerId.Value,
                AssignedTraderId = rfqCase.AssignedTraderId.Value,
                Owned = rfqCase.Owned,
            },
        };

        dbContext.RfqCases.Add(entity);
    }

    public async Task<RfqCase?> GetAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.RfqCases
            .Include(item => item.Revisions)
            .Include(item => item.Current)
            .ThenInclude(current => current.CurrentRevision)
            .SingleOrDefaultAsync(item => item.CaseId == caseId.Value, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var revisionEntity = entity.Current.CurrentRevision;
        var revision = RfqRevision.Restore(
            new RevisionId(revisionEntity.RevisionId),
            new CaseId(entity.CaseId),
            revisionEntity.Status,
            revisionEntity.Notional,
            revisionEntity.SettlementDate,
            revisionEntity.StandardSettlementDate,
            revisionEntity.SalesAndTradingMessage,
            revisionEntity.Version,
            revisionEntity.CreatedAt,
            UserId.Create(revisionEntity.CreatedBy),
            revisionEntity.ConfirmedAt,
            revisionEntity.ConfirmedBy is null
                ? null
                : UserId.Create(revisionEntity.ConfirmedBy));
        RfqLifecycle lifecycle = entity.Current.Lifecycle switch
        {
            RfqLifecycleKind.Draft => new DraftRfq(revision.RevisionId),
            RfqLifecycleKind.Open => new OpenRfq(
                revision.RevisionId,
                UserId.Create(entity.Current.ContactOwnerId),
                UserId.Create(entity.Current.AssignedTraderId),
                entity.Current.QuoteStatus
                    ?? throw new InvalidOperationException("Open RFQ is missing QuoteStatus."),
                entity.Current.QuoteRequestReason,
                entity.Current.Owned),
            _ => throw new InvalidOperationException("Unsupported RFQ lifecycle."),
        };

        return RfqCase.Restore(
            new CaseId(entity.CaseId),
            ClientId.Create(entity.ClientId),
            SecurityId.Create(entity.SecurityId),
            CategoryId.Create(entity.CategorySnapshot),
            entity.CreatedAt,
            UserId.Create(entity.CreatedBy),
            UserId.Create(entity.SalesId),
            UserId.Create(entity.Current.ContactOwnerId),
            UserId.Create(entity.Current.AssignedTraderId),
            entity.Current.Owned,
            revision,
            lifecycle);
    }

    public void Update(RfqCase rfqCase)
    {
        var entity = dbContext.RfqCases.Local
            .SingleOrDefault(item => item.CaseId == rfqCase.CaseId.Value)
            ?? throw new InvalidOperationException(
                "The RFQ Case must be loaded before it can be updated.");
        var revision = entity.Revisions.Single(item =>
            item.RevisionId == rfqCase.InitialRevision.RevisionId.Value);

        revision.Status = rfqCase.InitialRevision.Status;
        revision.Notional = rfqCase.InitialRevision.Notional;
        revision.SettlementDate = rfqCase.InitialRevision.SettlementDate;
        revision.SalesAndTradingMessage = rfqCase.InitialRevision.SalesAndTradingMessage;
        revision.Version = rfqCase.InitialRevision.Version;
        revision.ConfirmedAt = rfqCase.InitialRevision.ConfirmedAt;
        revision.ConfirmedBy = rfqCase.InitialRevision.ConfirmedBy?.Value;

        entity.Current.Lifecycle = rfqCase.Lifecycle is OpenRfq
            ? RfqLifecycleKind.Open
            : RfqLifecycleKind.Draft;
        entity.Current.RfqStatus = rfqCase.Status;
        entity.Current.QuoteStatus = rfqCase.QuoteStatus;
        entity.Current.QuoteRequestReason = rfqCase.QuoteRequestReason;
        entity.Current.CurrentRevisionId = rfqCase.InitialRevision.RevisionId.Value;
        entity.Current.ContactOwnerId = rfqCase.ContactOwnerId.Value;
        entity.Current.AssignedTraderId = rfqCase.AssignedTraderId.Value;
        entity.Current.Owned = rfqCase.Owned;
        entity.Current.Version++;
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
                && entity.Current.CurrentRevision.Status != RevisionStatus.Discarded
                && (entity.Current.Lifecycle == RfqLifecycleKind.Draft
                    || entity.Current.Lifecycle == RfqLifecycleKind.Open))
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenBy(entity => entity.CaseId)
            .Select(entity => new SalesRfqListItem(
                entity.CaseId,
                entity.ClientId,
                dbContext.Clients
                    .Where(client => client.ClientId == entity.ClientId)
                    .Select(client => client.Name)
                    .FirstOrDefault() ?? entity.ClientId,
                entity.SecurityId,
                dbContext.Securities
                    .Where(security => security.SecurityId == entity.SecurityId)
                    .Select(security => security.JapaneseName)
                    .FirstOrDefault() ?? entity.SecurityId,
                dbContext.Securities
                    .Where(security => security.SecurityId == entity.SecurityId)
                    .Select(security => security.BbgDisplay)
                    .FirstOrDefault() ?? entity.SecurityId,
                entity.CategorySnapshot,
                entity.Current.RfqStatus.ToString(),
                entity.Current.QuoteStatus == null
                    ? null
                    : entity.Current.QuoteStatus.Value.ToString(),
                entity.Current.QuoteRequestReason == null
                    ? null
                    : entity.Current.QuoteRequestReason.Value.ToString(),
                entity.Current.CurrentRevisionId,
                entity.Current.CurrentRevision.Status.ToString(),
                entity.Current.ContactOwnerId,
                entity.Current.AssignedTraderId,
                entity.Current.CurrentRevision.SettlementDate,
                entity.Current.CurrentRevision.StandardSettlementDate,
                entity.Current.CurrentRevision.Notional,
                entity.Current.CurrentRevision.SalesAndTradingMessage,
                entity.Current.CurrentRevision.Version,
                entity.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
