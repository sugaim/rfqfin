using Rfq.Domain;

namespace Rfq.Application;

public enum UserRole
{
    Sales,
    Trader,
}

public sealed record CurrentUser(
    UserId UserId,
    IReadOnlySet<UserRole> Roles,
    string DeskId);

public interface ICurrentUser
{
    CurrentUser User { get; }
}
