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
            CreatedBy = rfqCase.CreatedBy.Value,
            SalesId = rfqCase.SalesId.Value,
            CopiedFromCaseId = rfqCase.CopiedFromCaseId?.Value,
            Revisions = [revision],
            Memo = new CaseMemoEntity
            {
                CaseId = rfqCase.CaseId.Value,
                Version = 1,
            },
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
        var revision = ToDomainRevision(revisionEntity);
        var pendingDraft = entity.Revisions
            .Where(item => item.RevisionId != revisionEntity.RevisionId)
            .Where(item => item.Status == RevisionStatus.Draft)
            .Select(ToDomainRevision)
            .SingleOrDefault();
        RfqLifecycle lifecycle = entity.Current.Lifecycle switch
        {
            RfqLifecycleKind.Draft => new DraftRfq(revision.RevisionId),
            RfqLifecycleKind.Open => RestoreOpen(entity.Current, revision.RevisionId),
            RfqLifecycleKind.Cancelled => new CancelledRfq(revision.RevisionId),
            RfqLifecycleKind.Closed => new ClosedRfq(
                revision.RevisionId,
                new QuoteId(entity.Current.ClosedQuoteId
                    ?? throw new DomainInvariantException("Closed RFQ is missing ClosedQuoteId.")),
                entity.Current.RfqStatus),
            _ => throw new DomainInvariantException("Unsupported RFQ lifecycle."),
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
        var entity = dbContext.RfqCases.Local
            .SingleOrDefault(item => item.CaseId == rfqCase.CaseId.Value)
            ?? throw new InvalidOperationException(
                "The RFQ Case must be loaded before it can be updated.");
        var revision = entity.Revisions.SingleOrDefault(item =>
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
            var pendingDraft = entity.Revisions.SingleOrDefault(item =>
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
        entity.Current.ContactOwnerId = rfqCase.ContactOwnerId.Value;
        entity.Current.AssignedTraderId = rfqCase.AssignedTraderId.Value;
        entity.Current.Owned = rfqCase.Ownership is Owned;
        entity.Current.Version = rfqCase.Version.Value;
    }

    public void UpdateRevision(RfqRevision revision)
    {
        var entity = dbContext.RfqRevisions.Local.SingleOrDefault(item =>
            item.RevisionId == revision.RevisionId.Value)
            ?? throw new InvalidOperationException(
                "The RFQ Revision must be loaded before it can be updated.");
        ApplyRevision(entity, revision);
    }

    public async Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(
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
                entity.Current.CurrentQuoteId,
                entity.Current.ClosedQuoteId,
                entity.Current.Version,
                entity.Current.CurrentRevision.Status.ToString(),
                entity.Current.ContactOwnerId,
                entity.Current.AssignedTraderId,
                entity.Current.CurrentRevision.SettlementDate,
                entity.Current.CurrentRevision.StandardSettlementDate,
                entity.Current.CurrentRevision.Notional,
                entity.Current.CurrentRevision.SalesAndTradingMessage,
                entity.Memo.SalesMemo,
                entity.Memo.Version,
                entity.Current.CurrentRevision.Version,
                entity.CreatedAt,
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => (Guid?)revision.RevisionId)
                    .SingleOrDefault(),
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => (long?)revision.Version)
                    .SingleOrDefault(),
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => revision.SettlementDate)
                    .SingleOrDefault(),
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => revision.Notional)
                    .SingleOrDefault(),
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => revision.SalesAndTradingMessage)
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TraderRfqListItem>> GetActiveTraderRfqsAsync(
        string deskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deskId);

        var cases = await dbContext.RfqCases
            .AsNoTracking()
            .Include(entity => entity.Memo)
            .Include(entity => entity.Current)
            .ThenInclude(current => current.CurrentRevision)
            .Where(entity =>
                (entity.Current.Lifecycle == RfqLifecycleKind.Open
                    || entity.Current.Lifecycle == RfqLifecycleKind.Closed)
                && dbContext.MasterUsers.Any(user =>
                    user.UserId == entity.Current.AssignedTraderId
                    && user.DeskId == deskId))
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenBy(entity => entity.CaseId)
            .ToListAsync(cancellationToken);
        var clientIds = cases.Select(item => item.ClientId).Distinct().ToArray();
        var securityIds = cases.Select(item => item.SecurityId).Distinct().ToArray();
        var revisionIds = cases.Select(item => item.Current.CurrentRevisionId).ToArray();
        var displayQuoteIds = cases
            .Select(item => item.Current.CurrentQuoteId ?? item.Current.ClosedQuoteId)
            .Where(item => item != null)
            .Select(item => item!.Value)
            .ToArray();
        var clients = await dbContext.Clients
            .AsNoTracking()
            .Where(item => clientIds.Contains(item.ClientId))
            .ToDictionaryAsync(item => item.ClientId, item => item.Name, cancellationToken);
        var securities = await dbContext.Securities
            .AsNoTracking()
            .Where(item => securityIds.Contains(item.SecurityId))
            .ToDictionaryAsync(item => item.SecurityId, cancellationToken);
        var quotes = await dbContext.WorkingQuotes
            .AsNoTracking()
            .Where(item => revisionIds.Contains(item.RevisionId))
            .ToDictionaryAsync(item => item.RevisionId, cancellationToken);
        var confirmedQuotes = await dbContext.ConfirmedQuotes
            .AsNoTracking()
            .Where(item => displayQuoteIds.Contains(item.QuoteId))
            .ToDictionaryAsync(item => item.QuoteId, cancellationToken);

        return cases.Select(entity =>
        {
            clients.TryGetValue(entity.ClientId, out var clientName);
            securities.TryGetValue(entity.SecurityId, out var security);
            quotes.TryGetValue(entity.Current.CurrentRevisionId, out var quoteEntity);
            var quote = quoteEntity is null ? null : WorkingQuoteMapper.ToDomain(quoteEntity);
            ConfirmedQuoteEntity? confirmedQuote = null;
            var displayQuoteId = entity.Current.CurrentQuoteId ?? entity.Current.ClosedQuoteId;
            if (displayQuoteId is not null)
            {
                confirmedQuotes.TryGetValue(displayQuoteId.Value, out confirmedQuote);
            }
            return new TraderRfqListItem(
                entity.CaseId,
                entity.ClientId,
                clientName ?? entity.ClientId,
                entity.SecurityId,
                security?.JapaneseName ?? entity.SecurityId,
                security?.BbgDisplay ?? entity.SecurityId,
                entity.CategorySnapshot,
                entity.Current.RfqStatus.ToString(),
                entity.Current.QuoteStatus == null
                    ? null
                    : entity.Current.QuoteStatus.Value.ToString(),
                entity.Current.QuoteRequestReason == null
                    ? null
                    : entity.Current.QuoteRequestReason.Value.ToString(),
                entity.Current.CurrentRevisionId,
                entity.Current.CurrentQuoteId,
                entity.Current.ClosedQuoteId,
                confirmedQuote?.ConfirmedAt,
                confirmedQuote?.ExpiresAt,
                entity.Current.CurrentRevision.QuoteSeedRevisionId,
                entity.Current.ContactOwnerId,
                entity.Current.AssignedTraderId,
                entity.Current.Owned,
                entity.Current.Version,
                entity.Current.CurrentRevision.SettlementDate,
                entity.Current.CurrentRevision.Notional,
                quote?.Mode.ToString() ?? WorkingQuoteMode.Calculated.ToString(),
                quote?.Calculated,
                quote?.Manual,
                quote?.Version.Value ?? 0,
                entity.Memo.TraderMemo,
                entity.Memo.Version,
                entity.CreatedAt);
        }).ToArray();
    }

    public async Task<IReadOnlyList<ExpiredQuoteCandidate>> GetExpiredQuotesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        await dbContext.RfqCases.AsNoTracking()
            .Where(item => item.Current.Lifecycle == RfqLifecycleKind.Open
                && item.Current.QuoteStatus == QuoteStatus.Quoted
                && item.Current.CurrentQuoteId != null
                && dbContext.ConfirmedQuotes.Any(quote =>
                    quote.QuoteId == item.Current.CurrentQuoteId
                    && quote.ExpiresAt != null
                    && quote.ExpiresAt <= now))
            .Select(item => new ExpiredQuoteCandidate(
                item.CaseId, item.Current.CurrentQuoteId!.Value, item.Current.Version))
            .ToListAsync(cancellationToken);

    private static RfqLifecycleKind ToLifecycleKind(RfqLifecycle lifecycle) => lifecycle switch
    {
        DraftRfq => RfqLifecycleKind.Draft,
        OpenRfq => RfqLifecycleKind.Open,
        CancelledRfq => RfqLifecycleKind.Cancelled,
        ClosedRfq => RfqLifecycleKind.Closed,
        _ => throw new InvalidOperationException("Unsupported RFQ lifecycle."),
    };

    private static OpenRfq RestoreOpen(CaseCurrentEntity current, RevisionId revisionId)
    {
        var ownership = current.Owned ? (Ownership)new Owned() : new Unowned();
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
            throw new DomainInvariantException("Open RFQ has an invalid RFQ status.");
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
            new RevisionTerms(revision.Notional, revision.SettlementDate,
                revision.StandardSettlementDate, revision.SalesAndTradingMessage),
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

        var persisted = entity.Revisions.Single(item =>
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
