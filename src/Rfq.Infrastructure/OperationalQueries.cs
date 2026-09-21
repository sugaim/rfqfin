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
        if (!string.IsNullOrWhiteSpace(search.ClientId)) query = query.Where(item => item.ClientId == search.ClientId);
        if (!string.IsNullOrWhiteSpace(search.SecurityId)) query = query.Where(item => item.SecurityId == search.SecurityId);
        if (!string.IsNullOrWhiteSpace(search.CategoryId)) query = query.Where(item => item.CategorySnapshot == search.CategoryId);
        if (!string.IsNullOrWhiteSpace(search.ContactOwnerId)) query = query.Where(item => item.Current.ContactOwnerId == search.ContactOwnerId);
        if (!string.IsNullOrWhiteSpace(search.SalesId)) query = query.Where(item => item.SalesId == search.SalesId);
        if (!string.IsNullOrWhiteSpace(search.AssignedTraderId)) query = query.Where(item => item.Current.AssignedTraderId == search.AssignedTraderId);
        if (!string.IsNullOrWhiteSpace(search.Status)
            && Enum.TryParse<RfqStatus>(search.Status, true, out var status)) query = query.Where(item => item.Current.RfqStatus == status);
        if (search.CaseId is not null) query = query.Where(item => item.CaseId == search.CaseId);

        var rows = await query.OrderByDescending(item => item.CreatedAt).Take(ResultCap + 1)
            .Select(item => new PastRfqItem(
                item.CaseId, item.CreatedAt, item.ClientId,
                dbContext.Clients.Where(client => client.ClientId == item.ClientId)
                    .Select(client => client.Name).FirstOrDefault() ?? item.ClientId,
                item.SecurityId,
                dbContext.Securities.Where(security => security.SecurityId == item.SecurityId)
                    .Select(security => security.JapaneseName).FirstOrDefault() ?? item.SecurityId,
                item.CategorySnapshot, item.Current.RfqStatus.ToString(),
                item.Current.QuoteStatus == null ? null : item.Current.QuoteStatus.Value.ToString(),
                item.Current.ContactOwnerId, item.SalesId, item.Current.AssignedTraderId,
                item.Current.CurrentRevision.Notional, item.Current.CurrentRevision.SettlementDate))
            .ToListAsync(cancellationToken);
        return new(rows.Take(ResultCap).ToArray(), rows.Count > ResultCap);
    }

    public async Task<IReadOnlyList<RevisionHistoryItem>> GetRevisionHistoryAsync(
        long caseId, CancellationToken cancellationToken = default) =>
        await dbContext.RfqRevisions.AsNoTracking().Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new RevisionHistoryItem(item.RevisionId, item.Status.ToString(),
                item.Notional, item.SettlementDate, item.SalesAndTradingMessage, item.Version,
                item.CreatedAt, item.ConfirmedAt, item.CopiedFromRevisionId, item.QuoteSeedRevisionId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QuoteHistoryItem>> GetQuoteHistoryAsync(
        long caseId, CancellationToken cancellationToken = default) =>
        await (from quote in dbContext.ConfirmedQuotes.AsNoTracking()
               join revision in dbContext.RfqRevisions.AsNoTracking()
                   on quote.RevisionId equals revision.RevisionId
               where revision.CaseId == caseId
               orderby quote.ConfirmedAt descending
               select new QuoteHistoryItem(quote.QuoteId, quote.RevisionId, quote.Mode.ToString(),
                   quote.ConfirmedAt, quote.ExpiresAt, quote.RequestReasonAnswered.ToString()))
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
            .Select(group => new EodSummaryItem(group.Key,
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
