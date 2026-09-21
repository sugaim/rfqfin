using Rfq.Domain;

namespace Rfq.Application;

public sealed record CurrentUser(
    UserId UserId,
    IReadOnlySet<UserRole> Roles,
    DeskId DeskId);

public enum UserRole
{
    Sales,
    Trader,
}
