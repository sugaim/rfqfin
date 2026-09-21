using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public interface ICategoryRouting
{
    Task<UserId?> GetDefaultAssignedTraderAsync(
        CategoryId categoryId,
        CancellationToken cancellationToken = default);
}
