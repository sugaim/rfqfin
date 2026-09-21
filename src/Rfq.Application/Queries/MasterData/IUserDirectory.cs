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
