using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/masters")]
public sealed class MastersController(
    ISecuritySearch securitySearch,
    IClientSearch clientSearch,
    IUserDirectory userDirectory,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("securities/search")]
    public async Task<ActionResult<IReadOnlyList<SecuritySearchResult>>> SearchSecurities(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        return Ok(await securitySearch.SearchAsync(q, cancellationToken));
    }

    [HttpGet("clients/search")]
    public async Task<ActionResult<IReadOnlyList<ClientSearchResult>>> SearchClients(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        return Ok(await clientSearch.SearchAsync(q, cancellationToken));
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryResponse>>> GetUsers(
        [FromQuery] UserRole? role,
        CancellationToken cancellationToken)
    {
        var users = await userDirectory.GetUsersAsync(role, cancellationToken);
        return Ok(users
            .Where(user => user.DeskId == currentUser.User.DeskId)
            .Select(user => new UserSummaryResponse(
                user.UserId,
                user.Name,
                user.Roles.Select(item => item.ToString()).Order().ToArray(),
                user.DeskId)));
    }
}

public sealed record UserSummaryResponse(
    string UserId,
    string Name,
    IReadOnlyList<string> Roles,
    string DeskId);
