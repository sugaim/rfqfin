using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/current-user")]
public sealed class CurrentUserController(
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CurrentUserResponse>> Get(
        CancellationToken cancellationToken)
    {
        var user = await userDirectory.ResolveAsync(currentUser.User.UserId, cancellationToken);
        return Ok(new CurrentUserResponse(
            currentUser.User.UserId.Value,
            currentUser.User.Roles.Select(role => role.ToString()).Order().ToArray(),
            currentUser.User.DeskId.Value,
            user?.DefaultQuoteExpiryMinutes));
    }
}

public sealed record CurrentUserResponse(
    string UserId,
    IReadOnlyList<string> Roles,
    string DeskId,
    int? DefaultQuoteExpiryMinutes);
