using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

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
