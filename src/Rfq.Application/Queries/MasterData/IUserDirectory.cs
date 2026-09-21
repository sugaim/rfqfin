using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public interface IUserDirectory
{
    Task<IReadOnlyList<UserSummary>> GetUsersAsync(
        UserRole? role = null,
        CancellationToken cancellationToken = default);

    Task<UserSummary?> ResolveAsync(
        UserId userId,
        CancellationToken cancellationToken = default);
}

public sealed record UserSummary(
    UserId UserId,
    string Name,
    IReadOnlySet<UserRole> Roles,
    string DeskId,
    int? DefaultQuoteExpiryMinutes = null);
