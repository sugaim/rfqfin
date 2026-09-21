using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class PostgreSqlSecuritySearch(RfqDbContext dbContext) : ISecuritySearch
{
    private const int ResultLimit = 20;

    public async Task<IReadOnlyList<SecuritySearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var input = query.Trim();
        var normalizedBbg = SecuritySearchNormalizer.NormalizeBbgText(input);
        var normalizedInternalCode = SecuritySearchNormalizer.NormalizeInternalCode(input);
        var normalizedIsin = SecuritySearchNormalizer.NormalizeIsinPrefix(input);
        var candidates = new Dictionary<string, RankedSecurity>(StringComparer.Ordinal);

        await AddMatchesAsync(
            BaseQuery().Where(row => row.SecurityId == input),
            0,
            candidates,
            cancellationToken);

        if (normalizedInternalCode is not null)
        {
            await AddMatchesAsync(
                BaseQuery().Where(row => row.InternalCode == normalizedInternalCode),
                1,
                candidates,
                cancellationToken);
        }

        if (normalizedIsin is not null)
        {
            await AddMatchesAsync(
                BaseQuery().Where(row => row.Isin == normalizedIsin),
                2,
                candidates,
                cancellationToken);
            await AddMatchesAsync(
                BaseQuery().Where(row => row.Isin.StartsWith(normalizedIsin)),
                10,
                candidates,
                cancellationToken);
        }

        if (normalizedBbg.Length > 0)
        {
            await AddMatchesAsync(
                BaseQuery().Where(row => row.BbgSearchText == normalizedBbg),
                3,
                candidates,
                cancellationToken);
            await AddMatchesAsync(
                BaseQuery().Where(row => EF.Functions.ILike(row.BbgSearchText, $"%{normalizedBbg}%")),
                20,
                candidates,
                cancellationToken);
        }

        await AddMatchesAsync(
            BaseQuery().Where(row =>
                EF.Functions.ILike(row.JapaneseName, $"%{input}%")
                || EF.Functions.ILike(row.InternalCode, $"{input}%")),
            30,
            candidates,
            cancellationToken);

        return candidates.Values
            .OrderBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Result.InternalCode, StringComparer.Ordinal)
            .Take(ResultLimit)
            .Select(candidate => candidate.Result)
            .ToArray();
    }

    public async Task<SecuritySearchResult?> ResolveAsync(
        SecurityId securityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(securityId);

        var row = await BaseQuery()
            .Where(candidate => candidate.SecurityId == securityId.Value)
            .Select(candidate => new SecurityRow(
                candidate.SecurityId,
                candidate.JapaneseName,
                candidate.BbgDisplay,
                candidate.BbgSearchText,
                candidate.InternalCode,
                candidate.Isin,
                candidate.CategoryId,
                candidate.Category.Name))
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToResult(row);
    }

    private IQueryable<SecurityEntity> BaseQuery() => dbContext.Securities.AsNoTracking();

    private static async Task AddMatchesAsync(
        IQueryable<SecurityEntity> query,
        int rank,
        Dictionary<string, RankedSecurity> candidates,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .OrderBy(candidate => candidate.SecurityId)
            .Take(ResultLimit)
            .Select(candidate => new SecurityRow(
                candidate.SecurityId,
                candidate.JapaneseName,
                candidate.BbgDisplay,
                candidate.BbgSearchText,
                candidate.InternalCode,
                candidate.Isin,
                candidate.CategoryId,
                candidate.Category.Name))
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            var result = ToResult(row);
            if (!candidates.TryGetValue(result.SecurityId.Value, out var existing)
                || rank < existing.Rank)
            {
                candidates[result.SecurityId.Value] = new RankedSecurity(result, rank);
            }
        }
    }

    private static SecuritySearchResult ToResult(SecurityRow row) => new(
        SecurityId.Create(row.SecurityId),
        row.JapaneseName,
        row.BbgDisplay,
        row.InternalCode,
        row.Isin,
        CategoryId.Create(row.CategoryId),
        row.CategoryName);

    private sealed record SecurityRow(
        string SecurityId,
        string JapaneseName,
        string BbgDisplay,
        string BbgSearchText,
        string InternalCode,
        string Isin,
        string CategoryId,
        string CategoryName);

    private sealed record RankedSecurity(SecuritySearchResult Result, int Rank);
}

public sealed class PostgreSqlClientSearch(RfqDbContext dbContext) : IClientSearch
{
    public async Task<IReadOnlyList<ClientSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var input = query.Trim();
        return await dbContext.Clients
            .AsNoTracking()
            .Where(client =>
                EF.Functions.ILike(client.Code, $"%{input}%")
                || EF.Functions.ILike(client.Name, $"%{input}%"))
            .OrderBy(client => client.Code == input ? 0 : client.Code.StartsWith(input) ? 1 : 2)
            .ThenBy(client => client.Code)
            .Take(20)
            .Select(client => new ClientSearchResult(ClientId.Create(client.ClientId), client.Code, client.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientSearchResult?> ResolveAsync(
        ClientId clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        return await dbContext.Clients
            .AsNoTracking()
            .Where(client => client.ClientId == clientId.Value)
            .Select(client => new ClientSearchResult(ClientId.Create(client.ClientId), client.Code, client.Name))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

public sealed class PostgreSqlUserDirectory(RfqDbContext dbContext) : IUserDirectory
{
    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(
        UserRole? role = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.MasterUsers.AsNoTracking();
        if (role is not null)
        {
            var roleName = role.Value.ToString();
            query = query.Where(user => user.Roles.Contains(roleName));
        }

        var users = await query.OrderBy(user => user.Name).ToListAsync(cancellationToken);
        return users.Select(ToSummary).ToArray();
    }

    public async Task<UserSummary?> ResolveAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await dbContext.MasterUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == userId.Value,
                cancellationToken);
        return user is null ? null : ToSummary(user);
    }

    private static UserSummary ToSummary(MasterUserEntity user) => new(
        UserId.Create(user.UserId),
        user.Name,
        user.Roles
            .Select(role => Enum.Parse<UserRole>(role, ignoreCase: false))
            .ToHashSet(),
        DeskId.Create(user.DeskId),
        user.DefaultQuoteExpiryMinutes);
}

public sealed class PostgreSqlCategoryRouting(RfqDbContext dbContext) : ICategoryRouting
{
    public async Task<UserId?> GetDefaultAssignedTraderAsync(
        CategoryId categoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryId);

        var traderId = await dbContext.CategoryRoutings
            .AsNoTracking()
            .Where(routing => routing.CategoryId == categoryId.Value)
            .Select(routing => routing.DefaultTraderId)
            .SingleOrDefaultAsync(cancellationToken);
        return traderId is null ? null : UserId.Create(traderId);
    }
}

public sealed class MockStandardSettlementResolver : IStandardSettlementResolver
{
    public DateOnly Resolve(SecurityId securityId, DateOnly systemDate)
    {
        ArgumentNullException.ThrowIfNull(securityId);

        var result = systemDate;
        for (var businessDays = 0; businessDays < 2;)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                businessDays++;
            }
        }

        return result;
    }
}

public sealed class PostgreSqlSystemDateProvider(RfqDbContext dbContext) : ISystemDateProvider
{
    public async Task<DateOnly> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        var systemDate = await dbContext.SystemDates
            .AsNoTracking()
            .Where(item => item.Key == DevelopmentDataSeeder.SystemDateKey)
            .Select(item => (DateOnly?)item.BusinessDate)
            .SingleOrDefaultAsync(cancellationToken);
        return systemDate
            ?? throw new InvalidOperationException("The system date is not configured.");
    }
}

public sealed class PostgreSqlBusinessDateResolver(RfqDbContext dbContext)
    : IBusinessDateResolver
{
    public async Task<DateOnly> ResolveAsync(
        DateTimeOffset instant,
        DeskId deskId,
        CancellationToken cancellationToken = default)
    {
        var timeZoneId = await dbContext.Desks.AsNoTracking()
            .Where(item => item.DeskId == deskId.Value)
            .Select(item => item.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Desk '{deskId.Value}' was not found.");
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);
    }
}
