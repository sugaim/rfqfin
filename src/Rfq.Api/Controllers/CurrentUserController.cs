using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/current-user")]
public sealed class CurrentUserController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public ActionResult<CurrentUserResponse> Get() => Ok(new CurrentUserResponse(
        currentUser.User.UserId.Value,
        currentUser.User.Roles.Select(role => role.ToString()).Order().ToArray(),
        currentUser.User.DeskId));
}

public sealed record CurrentUserResponse(
    string UserId,
    IReadOnlyList<string> Roles,
    string DeskId);
