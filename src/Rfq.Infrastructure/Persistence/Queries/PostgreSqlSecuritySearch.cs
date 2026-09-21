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

