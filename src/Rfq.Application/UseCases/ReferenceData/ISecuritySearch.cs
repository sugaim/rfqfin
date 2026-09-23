using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public interface ISecuritySearch
{
    Task<IReadOnlyList<SecuritySearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<SecuritySearchResult?> ResolveAsync(
        SecurityId securityId,
        CancellationToken cancellationToken = default);
}

public sealed record SecuritySearchResult(
    SecurityId SecurityId,
    string JapaneseName,
    string BbgDisplay,
    string InternalCode,
    string Isin,
    CategoryId CategoryId,
    string CategoryName);
