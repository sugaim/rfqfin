using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public interface ICategoryRouting
{
    Task<UserId> GetDefaultAssignedTraderAsync(
        CategoryId categoryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryRoutingItem>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<CategoryRoutingItem> SetDefaultAssignedTraderAsync(
        CategoryId categoryId,
        UserId traderId,
        CancellationToken cancellationToken = default);
}

public sealed record CategoryRoutingItem(
    CategoryId CategoryId,
    string CategoryName,
    UserId DefaultTraderId,
    string DefaultTraderName);
