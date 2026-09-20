using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api;

public sealed class DevelopmentCurrentUser : ICurrentUser
{
    public CurrentUser User { get; } = new(
        UserId.Create("sales-dev"),
        new HashSet<UserRole> { UserRole.Sales },
        "jpy-credit");
}
