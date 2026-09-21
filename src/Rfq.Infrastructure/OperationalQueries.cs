using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class PostgreSqlOperationalQueries(
    RfqDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IOperationalQueries
{
    private const int ResultCap = 20_000;

    public async Task<PastRfqResult> SearchAsync(PastRfqSearch search,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.RfqCases.AsNoTracking()
            .Include(item => item.Current).ThenInclude(item => item.CurrentRevision)
            .Where(item => dbContext.MasterUsers.Any(user =>
                user.UserId == item.Current.AssignedTraderId
                && user.DeskId == currentUser.User.DeskId.Value));
        if (search.From is not null || search.To is not null)
        {
            var deskTimeZone = await ResolveDeskTimeZoneAsync(cancellationToken);
            if (search.From is not null)
            {
                var from = DeskDateBoundary.ToUtc(search.From.Value, deskTimeZone);
                query = query.Where(item => item.CreatedAt >= from);
            }
            if (search.To is not null)
            {
                var to = DeskDateBoundary.ToUtc(search.To.Value.AddDays(1), deskTimeZone);
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
            .Select(item => new PastRfqItem(
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
                item.Current.CurrentRevision.Notional, item.Current.CurrentRevision.SettlementDate))
            .ToListAsync(cancellationToken);
        return new(rows.Take(ResultCap).ToArray(), rows.Count > ResultCap);
    }

    public async Task<IReadOnlyList<RevisionHistoryItem>> GetRevisionHistoryAsync(
        CaseId caseId, CancellationToken cancellationToken = default) =>
        await dbContext.RfqRevisions.AsNoTracking().Where(item => item.CaseId == caseId.Value)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new RevisionHistoryItem(new RevisionId(item.RevisionId), item.Status,
                item.Notional, item.SettlementDate, item.SalesAndTradingMessage, new StateVersion(item.Version),
                item.CreatedAt, item.ConfirmedAt,
                item.CopiedFromRevisionId == null ? null : new RevisionId(item.CopiedFromRevisionId.Value),
                item.QuoteSeedRevisionId == null ? null : new RevisionId(item.QuoteSeedRevisionId.Value)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QuoteHistoryItem>> GetQuoteHistoryAsync(
        CaseId caseId, CancellationToken cancellationToken = default) =>
        await (from quote in dbContext.ConfirmedQuotes.AsNoTracking()
               join revision in dbContext.RfqRevisions.AsNoTracking()
                   on quote.RevisionId equals revision.RevisionId
               where revision.CaseId == caseId.Value
               orderby quote.ConfirmedAt descending
               select new QuoteHistoryItem(new QuoteId(quote.QuoteId), new RevisionId(quote.RevisionId), quote.Mode,
                   quote.ConfirmedAt, quote.ExpiresAt, quote.RequestReasonAnswered))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EodSummaryItem>> GetEodAsync(
        DateOnly date, CancellationToken cancellationToken = default)
    {
        var deskId = currentUser.User.DeskId.Value;
        var deskTimeZone = await ResolveDeskTimeZoneAsync(cancellationToken);
        var utcFrom = DeskDateBoundary.ToUtc(date, deskTimeZone);
        var utcTo = DeskDateBoundary.ToUtc(date.AddDays(1), deskTimeZone);
        var openRows = await dbContext.RfqCases.AsNoTracking()
            .Where(item => dbContext.MasterUsers.Any(user =>
                    user.UserId == item.Current.AssignedTraderId
                    && user.DeskId == deskId)
                && (item.Current.RfqStatus == RfqStatus.Active
                    || item.Current.RfqStatus == RfqStatus.Presented))
            .Select(item => item.Current.ContactOwnerId)
            .ToListAsync(cancellationToken);
        var closedHitType = RfqTransitionKind.ClosedHit.ToString();
        var closedAwayType = RfqTransitionKind.ClosedAway.ToString();
        var closeRows = await (
            from rfqEvent in dbContext.RfqEvents.AsNoTracking()
            join eventEntity in dbContext.Events.AsNoTracking()
                on rfqEvent.EventId equals eventEntity.EventId
            join rfq in dbContext.RfqCases.AsNoTracking()
                on rfqEvent.CaseId equals rfq.CaseId
            where eventEntity.OccurredAt >= utcFrom
                && eventEntity.OccurredAt < utcTo
                && (rfqEvent.Type == closedHitType || rfqEvent.Type == closedAwayType)
                && dbContext.MasterUsers.Any(user =>
                    user.UserId == rfq.Current.AssignedTraderId
                    && user.DeskId == deskId)
            select new
            {
                rfq.Current.ContactOwnerId,
                rfqEvent.Type,
            }).ToListAsync(cancellationToken);

        var ownerIds = openRows.Concat(closeRows.Select(item => item.ContactOwnerId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        return ownerIds.Select(ownerId => new EodSummaryItem(
            UserId.Create(ownerId),
            openRows.Count(item => item == ownerId),
            closeRows.Count(item => item.ContactOwnerId == ownerId && item.Type == closedHitType),
            closeRows.Count(item => item.ContactOwnerId == ownerId && item.Type == closedAwayType)))
            .ToArray();
    }

    public async Task<GridConfig?> GetGridConfigAsync(string screenId, string configKey,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.UserGridConfigs.AsNoTracking().SingleOrDefaultAsync(
            value => value.UserId == currentUser.User.UserId.Value
                && value.ScreenId == screenId && value.ConfigKey == configKey, cancellationToken);
        return item is null ? null : ToConfig(item);
    }

    public async Task<GridConfig> SaveGridConfigAsync(string screenId, string configKey,
        int version, string configJson, CancellationToken cancellationToken = default)
    {
        _ = System.Text.Json.JsonDocument.Parse(configJson);
        var userId = currentUser.User.UserId.Value;
        var item = await dbContext.UserGridConfigs.SingleOrDefaultAsync(value =>
            value.UserId == userId && value.ScreenId == screenId && value.ConfigKey == configKey,
            cancellationToken);
        if (item is null)
        {
            item = new() { UserId = userId, ScreenId = screenId, ConfigKey = configKey };
            dbContext.UserGridConfigs.Add(item);
        }
        item.Version = version;
        item.ConfigJson = configJson;
        item.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToConfig(item);
    }

    private static GridConfig ToConfig(UserGridConfigEntity item) => new(
        item.ScreenId, item.ConfigKey, item.Version, item.ConfigJson, item.UpdatedAt);

    private async Task<TimeZoneInfo> ResolveDeskTimeZoneAsync(
        CancellationToken cancellationToken)
    {
        var deskId = currentUser.User.DeskId.Value;
        var timeZoneId = await dbContext.Desks.AsNoTracking()
            .Where(item => item.DeskId == deskId)
            .Select(item => item.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Desk '{deskId}' was not found.");
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}

internal static class DeskDateBoundary
{
    public static DateTimeOffset ToUtc(DateOnly localDate, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var localMidnight = DateTime.SpecifyKind(
            localDate.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(localMidnight))
        {
            throw new InvalidOperationException(
                $"Local midnight {localDate:yyyy-MM-dd} does not exist in timezone '{timeZone.Id}'.");
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone));
    }
}
