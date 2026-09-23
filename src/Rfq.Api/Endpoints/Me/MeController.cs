using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Endpoints.CurrentUser;

[ApiController]
[Route("api/me")]
public sealed class MeController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [EndpointName("GetMe")]
    public MeResponse Get() => new(
        currentUser.User.UserId.Value,
        [.. currentUser.User.Roles.Select(MeApiMapper.ToApi).Order()],
        currentUser.User.DeskId.Value);
}

public enum UserRoleValue
{
    Sales,
    Trader,
}

public sealed record MeResponse(string UserId, IReadOnlyList<UserRoleValue> Roles, string DeskId);

public static class MeApiMapper
{
    public static UserRoleValue ToApi(UserRole value) => value switch
    {
        UserRole.Sales => UserRoleValue.Sales,
        UserRole.Trader => UserRoleValue.Trader,
        _ => throw new InvalidOperationException("Unknown User Role."),
    };
}
