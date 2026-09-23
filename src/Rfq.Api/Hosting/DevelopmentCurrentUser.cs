using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api;

public sealed class DevelopmentCurrentUser(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : ICurrentUser
{
    public const string HeaderName = "X-Development-User";
    public const string QueryParameterName = "developmentUser";

    public CurrentUser User
    {
        get
        {
            HttpRequest? request = httpContextAccessor.HttpContext?.Request;
            string? requestedUser = request?.Headers[HeaderName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(requestedUser))
            {
                requestedUser = request?.Query[QueryParameterName].FirstOrDefault();
            }
            string userId = string.IsNullOrWhiteSpace(requestedUser)
                ? configuration["DevelopmentIdentity:DefaultUserId"] ?? "sales-dev"
                : requestedUser;
            return userId switch
            {
                "sales-dev" => Create("sales-dev", UserRole.Sales),
                "sales-a" => Create("sales-a", UserRole.Sales),
                "trader-a" => Create("trader-a", UserRole.Trader),
                "trader-b" => Create("trader-b", UserRole.Trader),
                _ => throw new RfqForbiddenException(
                    $"Unknown development identity '{userId}'."),
            };
        }
    }

    private static CurrentUser Create(string userId, UserRole role) => new(
        UserId.Create(userId),
        new HashSet<UserRole> { role },
        DeskId.Create("jpy-credit"));
}
