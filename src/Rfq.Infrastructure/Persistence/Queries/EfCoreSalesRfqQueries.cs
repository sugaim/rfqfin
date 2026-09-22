using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreSalesRfqQueries(RfqDbContext dbContext) : ISalesRfqQueries
{
    private static readonly string[] StateRfqEvents =
    [
        nameof(RfqTransitionKind.RevisionConfirmed),
        nameof(RfqTransitionKind.Cancelled),
        nameof(RfqTransitionKind.Reopened),
        nameof(RfqTransitionKind.ClosedHit),
        nameof(RfqTransitionKind.ClosedAway),
        nameof(RfqTransitionKind.OutcomeCorrected),
    ];

    private static readonly string[] StateQuoteEvents =
    [
        nameof(QuoteTransitionKind.Confirmed),
        nameof(QuoteTransitionKind.Presented),
        nameof(QuoteTransitionKind.Unpresented),
        nameof(QuoteTransitionKind.Withdrawn),
        nameof(QuoteTransitionKind.Expired),
    ];

    public async Task<IReadOnlyList<SalesRfqListItem>> GetAsync(
        UserId salesUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(salesUserId);

        List<SalesRow> rows = await dbContext.RfqCases
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
            .Select(entity => new SalesRow(
                entity.CaseId,
                entity.ClientId,
                dbContext.Clients.Where(client => client.ClientId == entity.ClientId)
                    .Select(client => client.Name).FirstOrDefault() ?? entity.ClientId,
                entity.SecurityId,
                dbContext.Securities.Where(security => security.SecurityId == entity.SecurityId)
                    .Select(security => security.JapaneseName).FirstOrDefault() ?? entity.SecurityId,
                dbContext.Securities.Where(security => security.SecurityId == entity.SecurityId)
                    .Select(security => security.BbgDisplay).FirstOrDefault() ?? entity.SecurityId,
                entity.CategorySnapshot,
                entity.Current.RfqStatus,
                entity.Current.QuoteStatus,
                entity.Current.QuoteRequestReason,
                entity.Current.CurrentRevisionId,
                entity.Current.CurrentQuoteId,
                entity.Current.ClosedQuoteId,
                entity.Current.Version,
                entity.Current.CurrentRevision.Status,
                entity.SalesId,
                entity.Current.ContactOwnerId,
                entity.Current.AssignedTraderId,
                entity.Current.CurrentRevision.SettlementDate,
                entity.Current.CurrentRevision.StandardSettlementDate,
                entity.Current.CurrentRevision.Notional,
                entity.Current.CurrentRevision.SalesAndTradingMessage,
                entity.SalesMemo.Value,
                entity.SalesMemo.Version,
                entity.Current.CurrentRevision.Version,
                entity.CreatedAt,
                entity.Current.CurrentRevision.CreatedAt,
                entity.Current.CurrentRevision.ConfirmedAt,
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => (Guid?)revision.RevisionId).SingleOrDefault(),
                entity.Revisions
                    .Where(revision => revision.Status == RevisionStatus.Draft
                        && revision.RevisionId != entity.Current.CurrentRevisionId)
                    .Select(revision => (long?)revision.Version).SingleOrDefault(),
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

        if (rows.Count == 0)
        {
            return [];
        }

        long[] caseIds = [.. rows.Select(row => row.CaseId)];
        Guid[] quoteIds = [.. rows.Select(row => row.CurrentQuoteId ?? row.ClosedQuoteId)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct()];

        List<QuoteRow> quoteRows = quoteIds.Length == 0
            ? []
            : await dbContext.ConfirmedQuotes.AsNoTracking()
                .Where(quote => quoteIds.Contains(quote.QuoteId))
                .Select(quote => new QuoteRow(
                    quote.QuoteId,
                    quote.Mode,
                    quote.CalculatedPayloadJson,
                    quote.ManualPayloadJson,
                    quote.ConfirmedAt))
                .ToListAsync(cancellationToken);
        var quotes = quoteRows.ToDictionary(row => row.QuoteId, ToSummary);

        List<StateEvent> rfqStateEvents = await dbContext.RfqEvents.AsNoTracking()
            .Where(item => caseIds.Contains(item.CaseId) && StateRfqEvents.Contains(item.Type))
            .Select(item => new StateEvent(item.CaseId, item.Event.OccurredAt))
            .ToListAsync(cancellationToken);
        List<StateEvent> quoteStateEvents = await dbContext.QuoteEvents.AsNoTracking()
            .Where(item => StateQuoteEvents.Contains(item.Type)
                && dbContext.ConfirmedQuotes.Any(quote => quote.QuoteId == item.QuoteId
                    && caseIds.Contains(quote.Revision.CaseId)))
            .Select(item => new StateEvent(
                dbContext.ConfirmedQuotes.Where(quote => quote.QuoteId == item.QuoteId)
                    .Select(quote => quote.Revision.CaseId).Single(),
                item.Event.OccurredAt))
            .ToListAsync(cancellationToken);
        var stateSince = rfqStateEvents.Concat(quoteStateEvents)
            .GroupBy(item => item.CaseId)
            .ToDictionary(group => group.Key, group => group.Max(item => item.OccurredAt));

        return [.. rows.Select(row =>
        {
            SalesConfirmedQuoteSummary? quote = quotes.GetValueOrDefault(
                row.CurrentQuoteId ?? row.ClosedQuoteId ?? Guid.Empty);
            DateTimeOffset persistedStateStart = quote?.ConfirmedAt
                ?? row.CurrentRevisionConfirmedAt
                ?? row.CurrentRevisionCreatedAt;
            return new SalesRfqListItem(
                new CaseId(row.CaseId),
                ClientId.Create(row.ClientId),
                row.ClientName,
                SecurityId.Create(row.SecurityId),
                row.SecurityJapaneseName,
                row.SecurityBbgDisplay,
                CategoryId.Create(row.CategoryId),
                row.RfqStatus,
                row.QuoteStatus,
                row.QuoteRequestReason,
                new RevisionId(row.CurrentRevisionId),
                ToQuoteId(row.CurrentQuoteId),
                ToQuoteId(row.ClosedQuoteId),
                new StateVersion(row.CurrentVersion),
                row.RevisionStatus,
                row.SalesId is null ? null : UserId.Create(row.SalesId),
                UserId.Create(row.ContactOwnerId),
                UserId.Create(row.AssignedTraderId),
                row.SettlementDate,
                row.StandardSettlementDate,
                row.Notional,
                row.SalesAndTradingMessage,
                row.SalesMemo,
                new StateVersion(row.SalesMemoVersion),
                new StateVersion(row.Version),
                row.CreatedAt,
                stateSince.GetValueOrDefault(row.CaseId, persistedStateStart),
                quote,
                ToRevisionId(row.DraftRevisionId),
                ToStateVersion(row.DraftVersion),
                row.DraftSettlementDate,
                row.DraftNotional,
                row.DraftSalesAndTradingMessage);
        })];
    }

    private static SalesConfirmedQuoteSummary ToSummary(QuoteRow row)
    {
        if (row.Mode == WorkingQuoteMode.Calculated)
        {
            CalculatedQuotePayload payload = QuotePayloadPersistence.DeserializeCalculated(row.CalculatedPayloadJson
                ?? throw new DomainInvariantException("Calculated confirmed quote payload is missing."));
            return new SalesConfirmedQuoteSummary(
                new QuoteId(row.QuoteId),
                row.Mode,
                payload.Price,
                payload.BbgYield,
                payload.FinalSimpleYield,
                payload.GSpread,
                row.ConfirmedAt);
        }

        ManualQuotePayload manual = QuotePayloadPersistence.DeserializeManual(row.ManualPayloadJson
            ?? throw new DomainInvariantException("Manual confirmed quote payload is missing."));
        return new SalesConfirmedQuoteSummary(
            new QuoteId(row.QuoteId),
            row.Mode,
            manual.Price,
            null,
            manual.FinalSimpleYield,
            null,
            row.ConfirmedAt);
    }

    private static RevisionId? ToRevisionId(Guid? value) =>
        value is null ? null : new RevisionId(value.Value);

    private static QuoteId? ToQuoteId(Guid? value) => value is null ? null : new QuoteId(value.Value);

    private static StateVersion? ToStateVersion(long? value) =>
        value is null ? null : new StateVersion(value.Value);

    private sealed record StateEvent(long CaseId, DateTimeOffset OccurredAt);

    private sealed record QuoteRow(
        Guid QuoteId,
        WorkingQuoteMode Mode,
        string? CalculatedPayloadJson,
        string? ManualPayloadJson,
        DateTimeOffset ConfirmedAt);

    private sealed record SalesRow(
        long CaseId,
        string ClientId,
        string ClientName,
        string SecurityId,
        string SecurityJapaneseName,
        string SecurityBbgDisplay,
        string CategoryId,
        RfqStatus RfqStatus,
        QuoteStatus? QuoteStatus,
        QuoteRequestReason? QuoteRequestReason,
        Guid CurrentRevisionId,
        Guid? CurrentQuoteId,
        Guid? ClosedQuoteId,
        long CurrentVersion,
        RevisionStatus RevisionStatus,
        string? SalesId,
        string ContactOwnerId,
        string AssignedTraderId,
        DateOnly? SettlementDate,
        DateOnly StandardSettlementDate,
        decimal? Notional,
        string SalesAndTradingMessage,
        string SalesMemo,
        long SalesMemoVersion,
        long Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset CurrentRevisionCreatedAt,
        DateTimeOffset? CurrentRevisionConfirmedAt,
        Guid? DraftRevisionId,
        long? DraftVersion,
        DateOnly? DraftSettlementDate,
        decimal? DraftNotional,
        string? DraftSalesAndTradingMessage);
}
