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
                && user.DeskId == currentUser.User.DeskId));
        if (search.From is not null)
        {
            var from = new DateTimeOffset(search.From.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(item => item.CreatedAt >= from);
        }
        if (search.To is not null)
        {
            var to = new DateTimeOffset(search.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(item => item.CreatedAt < to);
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
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddDays(1);
        var rows = await dbContext.RfqCases.AsNoTracking()
            .Where(item => item.CreatedAt >= from && item.CreatedAt < to)
            .Select(item => new { item.Current.ContactOwnerId, item.Current.RfqStatus })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(item => item.ContactOwnerId)
            .Select(group => new EodSummaryItem(UserId.Create(group.Key),
                group.Count(item => item.RfqStatus is RfqStatus.Active or RfqStatus.Presented),
                group.Count(item => item.RfqStatus == RfqStatus.Hit),
                group.Count(item => item.RfqStatus == RfqStatus.Away)))
            .OrderBy(item => item.ContactOwnerId).ToArray();
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
}
