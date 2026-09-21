using Rfq.Domain;

namespace Rfq.Application;

public sealed record CurrentUser(
    UserId UserId,
    IReadOnlySet<UserRole> Roles,
    string DeskId);
