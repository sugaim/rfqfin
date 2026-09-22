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
            RevisionId = rfqCase.CurrentRevision.RevisionId.Value,
            CaseId = rfqCase.CaseId.Value,
            Status = rfqCase.CurrentRevision.Status,
            Version = rfqCase.CurrentRevision.Version.Value,
            CreatedAt = rfqCase.CurrentRevision.CreatedAt,
            CreatedBy = rfqCase.CurrentRevision.CreatedBy.Value,
            SettlementDate = rfqCase.CurrentRevision.SettlementDate,
            StandardSettlementDate = rfqCase.CurrentRevision.StandardSettlementDate,
            Notional = rfqCase.CurrentRevision.Notional,
            SalesAndTradingMessage = rfqCase.CurrentRevision.SalesAndTradingMessage,
            CopiedFromRevisionId = rfqCase.CurrentRevision.CopiedFromRevisionId?.Value,
            QuoteSeedRevisionId = rfqCase.CurrentRevision.QuoteSeedRevisionId?.Value,
            ConfirmedAt = rfqCase.CurrentRevision.ConfirmedAt,
            ConfirmedBy = rfqCase.CurrentRevision.ConfirmedBy?.Value,
        };

        var entity = new RfqCaseEntity
        {
            CaseId = rfqCase.CaseId.Value,
            ClientId = rfqCase.ClientId.Value,
            SecurityId = rfqCase.SecurityId.Value,
            CategorySnapshot = rfqCase.CategorySnapshot.Value,
            CreatedAt = rfqCase.CreatedAt,
            CreatedBusinessDate = rfqCase.CreatedBusinessDate,
            CreatedBy = rfqCase.CreatedBy.Value,
            SalesId = rfqCase.SalesId?.Value,
            CopiedFromCaseId = rfqCase.CopiedFromCaseId?.Value,
            Revisions = [revision],
            SalesMemo = new SalesMemoEntity { CaseId = rfqCase.CaseId.Value, Version = 1 },
            TraderMemo = new TraderMemoEntity { CaseId = rfqCase.CaseId.Value, Version = 1 },
            Current = new CaseCurrentEntity
            {
                CaseId = rfqCase.CaseId.Value,
                Lifecycle = ToLifecycleKind(rfqCase.Lifecycle),
                RfqStatus = rfqCase.Status,
                QuoteStatus = rfqCase.QuoteStatus,
                QuoteRequestReason = rfqCase.QuoteRequestReason,
                CurrentRevisionId = revision.RevisionId,
                CurrentRevision = revision,
                CurrentQuoteId = rfqCase.CurrentQuoteId?.Value,
                ClosedQuoteId = rfqCase.ClosedQuoteId?.Value,
                ClosedBusinessDate = rfqCase.ClosedBusinessDate,
                Version = rfqCase.Version.Value,
                ContactOwnerId = rfqCase.ContactOwnerId.Value,
                AssignedTraderId = rfqCase.AssignedTraderId.Value,
                Owned = rfqCase.Ownership is Owned,
            },
        };

        dbContext.RfqCases.Add(entity);
    }

    public async Task<RfqCase?> GetAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default)
    {
        RfqCaseEntity? entity = await dbContext.RfqCases
            .Include(item => item.Revisions)
            .Include(item => item.Current)
            .ThenInclude(current => current.CurrentRevision)
            .SingleOrDefaultAsync(item => item.CaseId == caseId.Value, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        RfqRevisionEntity revisionEntity = entity.Current.CurrentRevision;
        RfqRevision revision = ToDomainRevision(revisionEntity);
        RfqRevision? pendingDraft = entity.Revisions
            .Where(item => item.RevisionId != revisionEntity.RevisionId)
            .Where(item => item.Status == RevisionStatus.Draft)
            .Select(ToDomainRevision)
            .SingleOrDefault();
        RfqLifecycle lifecycle = entity.Current.Lifecycle switch
        {
            RfqLifecycleKind.Draft => new DraftRfq(revision.RevisionId),
            RfqLifecycleKind.Open => RestoreOpen(entity.Current, revision.RevisionId),
            RfqLifecycleKind.Cancelled => new CancelledRfq(revision.RevisionId),
            RfqLifecycleKind.Closed => RestoreClosed(entity.Current, revision.RevisionId),
            _ => throw new DomainInvariantException("Unsupported RFQ lifecycle."),
        };

        return RfqCase.Restore(
            new CaseId(entity.CaseId),
            ClientId.Create(entity.ClientId),
            SecurityId.Create(entity.SecurityId),
            CategoryId.Create(entity.CategorySnapshot),
            entity.CreatedAt,
            entity.CreatedBusinessDate,
            UserId.Create(entity.CreatedBy),
            entity.SalesId is null ? null : UserId.Create(entity.SalesId),
            UserId.Create(entity.Current.ContactOwnerId),
            UserId.Create(entity.Current.AssignedTraderId),
            new StateVersion(entity.Current.Version),
            revision,
            lifecycle,
            pendingDraft,
            entity.CopiedFromCaseId is null
                ? null
                : new CaseId(entity.CopiedFromCaseId.Value));
    }

    public void Update(RfqCase rfqCase)
    {
        RfqCaseEntity entity = dbContext.RfqCases.Local
            .SingleOrDefault(item => item.CaseId == rfqCase.CaseId.Value)
            ?? throw new InvalidOperationException(
                "The RFQ Case must be loaded before it can be updated.");
        RfqRevisionEntity? revision = entity.Revisions.SingleOrDefault(item =>
            item.RevisionId == rfqCase.CurrentRevision.RevisionId.Value);
        if (revision is null)
        {
            revision = ToEntity(rfqCase.CurrentRevision);
            entity.Revisions.Add(revision);
        }
        else
        {
            ApplyRevision(revision, rfqCase.CurrentRevision);
        }

        if (rfqCase.PendingDraftRevision is not null)
        {
            RfqRevisionEntity? pendingDraft = entity.Revisions.SingleOrDefault(item =>
                item.RevisionId == rfqCase.PendingDraftRevision.RevisionId.Value);
            if (pendingDraft is null)
            {
                entity.Revisions.Add(ToEntity(rfqCase.PendingDraftRevision));
            }
            else
            {
                ApplyRevision(pendingDraft, rfqCase.PendingDraftRevision);
            }
        }

        entity.Current.Lifecycle = ToLifecycleKind(rfqCase.Lifecycle);
        entity.Current.RfqStatus = rfqCase.Status;
        entity.Current.QuoteStatus = rfqCase.QuoteStatus;
        entity.Current.QuoteRequestReason = rfqCase.QuoteRequestReason;
        entity.Current.CurrentRevisionId = rfqCase.CurrentRevision.RevisionId.Value;
        entity.Current.CurrentQuoteId = rfqCase.CurrentQuoteId?.Value;
        entity.Current.ClosedQuoteId = rfqCase.ClosedQuoteId?.Value;
        entity.Current.ClosedBusinessDate = rfqCase.ClosedBusinessDate;
        entity.Current.ContactOwnerId = rfqCase.ContactOwnerId.Value;
        entity.Current.AssignedTraderId = rfqCase.AssignedTraderId.Value;
        entity.Current.Owned = rfqCase.Ownership is Owned;
        entity.Current.Version = rfqCase.Version.Value;
    }

    public void UpdateRevision(RfqRevision revision)
    {
        RfqRevisionEntity entity = dbContext.RfqRevisions.Local.SingleOrDefault(item =>
            item.RevisionId == revision.RevisionId.Value)
            ?? throw new InvalidOperationException(
                "The RFQ Revision must be loaded before it can be updated.");
        ApplyRevision(entity, revision);
    }

    private static RfqLifecycleKind ToLifecycleKind(RfqLifecycle lifecycle) => lifecycle switch
    {
        DraftRfq => RfqLifecycleKind.Draft,
        OpenRfq => RfqLifecycleKind.Open,
        CancelledRfq => RfqLifecycleKind.Cancelled,
        ClosedRfq => RfqLifecycleKind.Closed,
        _ => throw new RfqInvariantException("Unsupported RFQ lifecycle."),
    };

    private static ClosedRfq RestoreClosed(CaseCurrentEntity current, RevisionId revisionId)
    {
        var quoteId = new QuoteId(current.ClosedQuoteId
            ?? throw new DomainInvariantException("Closed RFQ is missing ClosedQuoteId."));
        DateOnly businessDate = current.ClosedBusinessDate
            ?? throw new DomainInvariantException(
                "Closed RFQ is missing ClosedBusinessDate.");
        return current.RfqStatus switch
        {
            RfqStatus.Hit => new HitRfq(revisionId, quoteId, businessDate),
            RfqStatus.Away => new AwayRfq(revisionId, quoteId, businessDate),
            _ => throw new DomainInvariantException("Closed RFQ has an invalid RFQ status."),
        };
    }

    private static OpenRfq RestoreOpen(CaseCurrentEntity current, RevisionId revisionId)
    {
        Ownership ownership = current.Owned ? (Ownership)new Owned() : new Unowned();
        if (current.RfqStatus == RfqStatus.Presented)
        {
            return new PresentedRfq(
                revisionId,
                ownership,
                new QuoteId(current.CurrentQuoteId
                    ?? throw new DomainInvariantException(
                        "Presented RFQ is missing CurrentQuoteId.")));
        }

        if (current.RfqStatus != RfqStatus.Active)
        {
            throw new DomainInvariantException("Open RFQ has an invalid RFQ status.");
        }

        ActiveQuoteState quoteState = current.QuoteStatus switch
        {
            QuoteStatus.Requested => new QuoteRequested(
                current.QuoteRequestReason
                    ?? throw new DomainInvariantException(
                        "Requested RFQ is missing QuoteRequestReason.")),
            QuoteStatus.Quoted => new QuoteConfirmed(
                new QuoteId(current.CurrentQuoteId
                    ?? throw new DomainInvariantException(
                        "Quoted RFQ is missing CurrentQuoteId."))),
            _ => throw new DomainInvariantException("Open RFQ is missing QuoteStatus."),
        };
        return new ActiveRfq(revisionId, ownership, quoteState);
    }

    private static RfqRevision ToDomainRevision(RfqRevisionEntity revision) =>
        RfqRevision.Restore(
            new RevisionId(revision.RevisionId),
            new CaseId(revision.CaseId),
            revision.Status,
            new RevisionTerms(
                revision.Notional,
                revision.SettlementDate,
                revision.StandardSettlementDate,
                revision.SalesAndTradingMessage),
            revision.CopiedFromRevisionId is null
                ? null
                : new RevisionId(revision.CopiedFromRevisionId.Value),
            revision.QuoteSeedRevisionId is null
                ? null
                : new RevisionId(revision.QuoteSeedRevisionId.Value),
            new StateVersion(revision.Version),
            revision.CreatedAt,
            UserId.Create(revision.CreatedBy),
            revision.ConfirmedAt,
            revision.ConfirmedBy is null ? null : UserId.Create(revision.ConfirmedBy));

    private static RfqRevisionEntity ToEntity(RfqRevision revision) => new()
    {
        RevisionId = revision.RevisionId.Value,
        CaseId = revision.CaseId.Value,
        Status = revision.Status,
        Notional = revision.Notional,
        SettlementDate = revision.SettlementDate,
        StandardSettlementDate = revision.StandardSettlementDate,
        SalesAndTradingMessage = revision.SalesAndTradingMessage,
        CopiedFromRevisionId = revision.CopiedFromRevisionId?.Value,
        QuoteSeedRevisionId = revision.QuoteSeedRevisionId?.Value,
        Version = revision.Version.Value,
        CreatedAt = revision.CreatedAt,
        CreatedBy = revision.CreatedBy.Value,
        ConfirmedAt = revision.ConfirmedAt,
        ConfirmedBy = revision.ConfirmedBy?.Value,
    };

    private static void ApplyChangedRevision(
        RfqCaseEntity entity,
        RfqRevision? changedRevision)
    {
        if (changedRevision is null)
        {
            return;
        }

        RfqRevisionEntity persisted = entity.Revisions.Single(item =>
            item.RevisionId == changedRevision.RevisionId.Value);
        ApplyRevision(persisted, changedRevision);
    }

    private static void ApplyRevision(
        RfqRevisionEntity entity,
        RfqRevision revision)
    {
        entity.Status = revision.Status;
        entity.Notional = revision.Notional;
        entity.SettlementDate = revision.SettlementDate;
        entity.SalesAndTradingMessage = revision.SalesAndTradingMessage;
        entity.Version = revision.Version.Value;
        entity.ConfirmedAt = revision.ConfirmedAt;
        entity.ConfirmedBy = revision.ConfirmedBy?.Value;
    }
}
