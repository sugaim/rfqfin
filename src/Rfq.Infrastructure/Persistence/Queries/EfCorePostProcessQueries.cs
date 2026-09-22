using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCorePostProcessQueries(RfqDbContext dbContext)
    : IPostProcessQueries
{
    private static readonly string[] TodayEventTypes =
    [
        EventPersistenceTypeCodes.Rfq.ClosedHit,
        EventPersistenceTypeCodes.Rfq.ClosedAway,
        EventPersistenceTypeCodes.Rfq.Cancelled,
        EventPersistenceTypeCodes.Rfq.OutcomeCorrected,
    ];

    public async Task<IReadOnlyList<PostProcessWorklistItem>> GetAsync(
        PostProcessPreset preset,
        PostProcessScope scope,
        DateOnly currentBusinessDate,
        CurrentUser currentUser,
        IReadOnlySet<CaseId> permittedCaseIds,
        CancellationToken cancellationToken = default)
    {
        long[] permitted = [.. permittedCaseIds.Select(value => value.Value)];
        IQueryable<RfqCaseEntity> query = dbContext.RfqCases
            .AsNoTracking()
            .Include(entity => entity.Current)
            .ThenInclude(current => current.CurrentRevision)
            .Include(entity => entity.SalesMemo)
            .Include(entity => entity.TraderMemo)
            .Where(entity => permitted.Contains(entity.CaseId)
                && entity.Current.RfqStatus != RfqStatus.Draft);

        query = preset switch
        {
            PostProcessPreset.Today => query.Where(entity =>
                entity.CreatedBusinessDate == currentBusinessDate
                || dbContext.RfqEvents.Any(rfqEvent =>
                    rfqEvent.CaseId == entity.CaseId
                    && rfqEvent.BusinessDate == currentBusinessDate
                    && TodayEventTypes.Contains(rfqEvent.Type))),
            PostProcessPreset.Unclosed => query.Where(entity =>
                entity.Current.RfqStatus == RfqStatus.Active
                || entity.Current.RfqStatus == RfqStatus.Presented),
            _ => throw new RfqRequestValidationException(
                "Unsupported Post Process preset."),
        };

        List<RfqCaseEntity> cases = await query
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenBy(entity => entity.CaseId)
            .ToListAsync(cancellationToken);
        if (scope == PostProcessScope.Mine)
        {
            cases = [.. cases.Where(entity => PostProcessOwnership.IsMine(
                currentUser.UserId,
                entity.SalesId is null ? null : UserId.Create(entity.SalesId),
                UserId.Create(entity.Current.ContactOwnerId),
                UserId.Create(entity.Current.AssignedTraderId)))];
        }
        if (cases.Count == 0)
        {
            return [];
        }

        long[] caseIds = [.. cases.Select(entity => entity.CaseId)];
        string[] clientIds = [.. cases.Select(entity => entity.ClientId).Distinct()];
        string[] securityIds = [.. cases.Select(entity => entity.SecurityId).Distinct()];
        Guid[] quoteIds = [.. cases
            .Select(entity => entity.Current.ClosedQuoteId ?? entity.Current.CurrentQuoteId)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()];

        Dictionary<string, string> clients = await dbContext.Clients
            .AsNoTracking()
            .Where(entity => clientIds.Contains(entity.ClientId))
            .ToDictionaryAsync(
                entity => entity.ClientId,
                entity => entity.Name,
                cancellationToken);
        Dictionary<string, SecurityEntity> securities = await dbContext.Securities
            .AsNoTracking()
            .Where(entity => securityIds.Contains(entity.SecurityId))
            .ToDictionaryAsync(
                entity => entity.SecurityId,
                cancellationToken);
        Dictionary<Guid, ConfirmedQuoteEntity> quotes = quoteIds.Length == 0
            ? []
            : await dbContext.ConfirmedQuotes
                .AsNoTracking()
                .Where(entity => quoteIds.Contains(entity.QuoteId))
                .ToDictionaryAsync(entity => entity.QuoteId, cancellationToken);
        List<RfqEventEntity> events = await dbContext.RfqEvents
            .AsNoTracking()
            .Include(entity => entity.Event)
            .Where(entity => caseIds.Contains(entity.CaseId))
            .ToListAsync(cancellationToken);
        var lastEvents = events
            .GroupBy(entity => entity.CaseId)
            .ToDictionary(
                group => group.Key,
                group => group.MaxBy(entity => entity.Event.OccurredAt)!);
        var correctionReasons = events
            .Where(entity => entity.Type == EventPersistenceTypeCodes.Rfq.OutcomeCorrected)
            .GroupBy(entity => entity.CaseId)
            .ToDictionary(
                group => group.Key,
                group => PersistenceJsonSerializer.Deserialize<OutcomeCorrectedPayload>(
                    group.MaxBy(entity => entity.Event.OccurredAt)!.PayloadJson,
                    "outcome-corrected event payload").Reason);

        bool useSalesMemo = currentUser.Roles.Contains(UserRole.Sales);
        return [.. cases.Select(entity =>
        {
            clients.TryGetValue(entity.ClientId, out string? clientName);
            securities.TryGetValue(entity.SecurityId, out SecurityEntity? security);
            Guid? quoteId = entity.Current.ClosedQuoteId ?? entity.Current.CurrentQuoteId;
            ConfirmedQuoteEntity? quote = quoteId.HasValue
                ? quotes.GetValueOrDefault(quoteId.Value)
                : null;
            var values = QuoteValues.From(quote);
            RfqEventEntity? last = lastEvents.GetValueOrDefault(entity.CaseId);
            return new PostProcessWorklistItem(
                new CaseId(entity.CaseId),
                entity.CreatedAt,
                entity.CreatedBusinessDate
                    ?? throw new DomainInvariantException(
                        "Operational RFQ is missing CreatedBusinessDate."),
                ClientId.Create(entity.ClientId),
                clientName ?? entity.ClientId,
                SecurityId.Create(entity.SecurityId),
                security?.JapaneseName ?? entity.SecurityId,
                security?.BbgDisplay ?? entity.SecurityId,
                entity.Current.CurrentRevision.Notional,
                entity.Current.CurrentRevision.SettlementDate,
                UserId.Create(entity.Current.ContactOwnerId),
                entity.SalesId is null ? null : UserId.Create(entity.SalesId),
                UserId.Create(entity.Current.AssignedTraderId),
                entity.Current.RfqStatus,
                new StateVersion(entity.Current.Version),
                entity.Current.CurrentRevision.SalesAndTradingMessage,
                useSalesMemo ? entity.SalesMemo.Value : entity.TraderMemo.Value,
                new StateVersion(useSalesMemo
                    ? entity.SalesMemo.Version
                    : entity.TraderMemo.Version),
                values.Price,
                values.FinalSimpleYield,
                values.Yield,
                values.Ysc,
                values.GSpread,
                entity.Current.ClosedBusinessDate,
                correctionReasons.GetValueOrDefault(entity.CaseId),
                last?.Event.ActorUserId is null
                    ? null
                    : UserId.Create(last.Event.ActorUserId),
                last?.Event.OccurredAt);
        })];
    }

    private sealed record QuoteValues(
        decimal? Price,
        decimal? FinalSimpleYield,
        decimal? Yield,
        decimal? Ysc,
        decimal? GSpread)
    {
        public static QuoteValues From(ConfirmedQuoteEntity? entity)
        {
            if (entity is null)
            {
                return new(null, null, null, null, null);
            }

            if (entity.Mode == WorkingQuoteMode.Manual)
            {
                ManualQuotePayload payload = QuotePayloadPersistence.DeserializeManual(
                    entity.ManualPayloadJson
                        ?? throw new DomainInvariantException(
                            "Manual confirmed quote payload is missing."));
                return new(
                    payload.Price,
                    payload.FinalSimpleYield,
                    null,
                    null,
                    null);
            }

            CalculatedQuotePayload calculated =
                QuotePayloadPersistence.DeserializeCalculated(
                    entity.CalculatedPayloadJson
                        ?? throw new DomainInvariantException(
                            "Calculated confirmed quote payload is missing."));
            return new(
                calculated.Price,
                calculated.FinalSimpleYield,
                calculated.BbgYield,
                calculated.Ysc,
                calculated.GSpread);
        }
    }
}
