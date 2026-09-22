using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreRfqSearchQueries(
    RfqDbContext dbContext,
    ICurrentUser currentUser) : IRfqSearchQueries
{
    private const int ResultCap = 20_000;

    public async Task<RfqSearchResult> SearchAsync(
        RfqSearch search,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.RfqCases.AsNoTracking()
            .Include(item => item.Current).ThenInclude(item => item.CurrentRevision)
            .Where(item => dbContext.MasterUsers.Any(user =>
                user.UserId == item.Current.AssignedTraderId
                && user.DeskId == currentUser.User.DeskId.Value));
        if (search.CreatedFrom is not null || search.CreatedTo is not null)
        {
            var deskTimeZone = await ResolveDeskTimeZoneAsync(cancellationToken);
            if (search.CreatedFrom is not null)
            {
                var from = DeskDateBoundary.ToUtc(search.CreatedFrom.Value, deskTimeZone);
                query = query.Where(item => item.CreatedAt >= from);
            }
            if (search.CreatedTo is not null)
            {
                var to = DeskDateBoundary.ToUtc(search.CreatedTo.Value.AddDays(1), deskTimeZone);
                query = query.Where(item => item.CreatedAt < to);
            }
        }
        if (search.ClientId is not null) query = query.Where(item => item.ClientId == search.ClientId.Value);
        if (search.SecurityId is not null) query = query.Where(item => item.SecurityId == search.SecurityId.Value);
        if (search.CategoryId is not null) query = query.Where(item => item.CategorySnapshot == search.CategoryId.Value);
        if (search.ContactOwnerId is not null) query = query.Where(item => item.Current.ContactOwnerId == search.ContactOwnerId.Value);
        if (search.SalesId is not null) query = query.Where(item => item.SalesId == search.SalesId.Value);
        if (search.AssignedTraderId is not null) query = query.Where(item => item.Current.AssignedTraderId == search.AssignedTraderId.Value);
        if (search.Status is not null) query = query.Where(item => item.Current.RfqStatus == search.Status.Value);
        if (search.CaseId is not null) query = query.Where(item => item.CaseId == search.CaseId.Value.Value);

        var rows = await query.OrderByDescending(item => item.CreatedAt).Take(ResultCap + 1)
            .Select(item => new RfqSearchItem(
                new CaseId(item.CaseId), item.CreatedAt, ClientId.Create(item.ClientId),
                dbContext.Clients.Where(client => client.ClientId == item.ClientId)
                    .Select(client => client.Name).FirstOrDefault() ?? item.ClientId,
                SecurityId.Create(item.SecurityId),
                dbContext.Securities.Where(security => security.SecurityId == item.SecurityId)
                    .Select(security => security.JapaneseName).FirstOrDefault() ?? item.SecurityId,
                CategoryId.Create(item.CategorySnapshot), item.Current.RfqStatus,
                item.Current.QuoteStatus,
                UserId.Create(item.Current.ContactOwnerId),
                item.SalesId == null ? null : UserId.Create(item.SalesId),
                UserId.Create(item.Current.AssignedTraderId),
                item.Current.CurrentRevision.Notional, item.Current.CurrentRevision.SettlementDate,
                null, null, null))
            .ToListAsync(cancellationToken);
        var cappedRows = rows.Take(ResultCap).ToArray();
        var caseIds = cappedRows.Select(item => item.CaseId.Value).ToArray();
        var quoteRefs = await dbContext.RfqCases.AsNoTracking()
            .Where(item => caseIds.Contains(item.CaseId))
            .Select(item => new SearchQuoteRef(item.CaseId,
                item.Current.CurrentRevisionId,
                item.Current.CurrentQuoteId ?? item.Current.ClosedQuoteId))
            .ToListAsync(cancellationToken);
        var revisionIds = quoteRefs.Select(item => item.RevisionId).Distinct().ToArray();
        var quoteIds = quoteRefs.Where(item => item.QuoteId != null)
            .Select(item => item.QuoteId!.Value).Distinct().ToArray();
        var workingQuotes = await dbContext.WorkingQuotes.AsNoTracking()
            .Where(item => revisionIds.Contains(item.RevisionId))
            .ToDictionaryAsync(item => item.RevisionId, cancellationToken);
        var confirmedQuotes = await dbContext.ConfirmedQuotes.AsNoTracking()
            .Where(item => quoteIds.Contains(item.QuoteId))
            .ToDictionaryAsync(item => item.QuoteId, cancellationToken);
        var quoteRefsByCase = quoteRefs.ToDictionary(item => item.CaseId);

        var projected = cappedRows.Select(row =>
        {
            var reference = quoteRefsByCase[row.CaseId.Value];
            QuoteSummary summary;
            if (reference.QuoteId is { } quoteId
                && confirmedQuotes.TryGetValue(quoteId, out var confirmed))
            {
                summary = ToSummary(confirmed.Mode, confirmed.CalculatedPayloadJson,
                    confirmed.ManualPayloadJson);
            }
            else if (workingQuotes.TryGetValue(reference.RevisionId, out var working))
            {
                summary = ToSummary(working.Mode, working.CalculatedPayloadJson,
                    working.ManualPayloadJson);
            }
            else
            {
                summary = new QuoteSummary(null, null, null);
            }

            return row with
            {
                Price = summary.Price,
                FinalSimpleYield = summary.FinalSimpleYield,
                Ysc = summary.Ysc,
            };
        }).ToArray();
        return new(projected, rows.Count > ResultCap);
    }

    private async Task<TimeZoneInfo> ResolveDeskTimeZoneAsync(
        CancellationToken cancellationToken)
    {
        var deskId = currentUser.User.DeskId.Value;
        var timeZoneId = await dbContext.Desks.AsNoTracking()
            .Where(item => item.DeskId == deskId)
            .Select(item => item.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RfqInvariantException($"Desk '{deskId}' was not found.");
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    private static QuoteSummary ToSummary(
        WorkingQuoteMode mode,
        string? calculatedJson,
        string? manualJson)
    {
        if (mode == WorkingQuoteMode.Manual)
        {
            var manual = QuotePayloadPersistence.DeserializeManual(manualJson
                ?? throw new DomainInvariantException("Manual quote payload is missing."));
            return new QuoteSummary(manual.Price, manual.FinalSimpleYield, null);
        }

        if (calculatedJson is null)
            return new QuoteSummary(null, null, null);
        var calculated = QuotePayloadPersistence.DeserializeCalculated(calculatedJson);
        return new QuoteSummary(calculated.Price, calculated.FinalSimpleYield, calculated.Ysc);
    }

    private sealed record SearchQuoteRef(long CaseId, Guid RevisionId, Guid? QuoteId);
    private sealed record QuoteSummary(decimal? Price, decimal? FinalSimpleYield, decimal? Ysc);
}
