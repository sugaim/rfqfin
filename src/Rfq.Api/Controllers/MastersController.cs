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
    public async Task<ActionResult<IReadOnlyList<SecuritySearchResponse>>> SearchSecurities(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        var results = await securitySearch.SearchAsync(q, cancellationToken);
        return Ok(results.Select(item => new SecuritySearchResponse(
            item.SecurityId.Value, item.JapaneseName, item.BbgDisplay,
            item.InternalCode, item.Isin, item.CategoryId.Value, item.CategoryName)));
    }

    [HttpGet("clients/search")]
    public async Task<ActionResult<IReadOnlyList<ClientSearchResponse>>> SearchClients(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        var results = await clientSearch.SearchAsync(q, cancellationToken);
        return Ok(results.Select(item => new ClientSearchResponse(
            item.ClientId.Value, item.Code, item.Name)));
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
                user.UserId.Value,
                user.Name,
                user.Roles.Select(item => item.ToString()).Order().ToArray(),
                user.DeskId,
                user.DefaultQuoteExpiryMinutes)));
    }
}

public sealed record UserSummaryResponse(
    string UserId,
    string Name,
    IReadOnlyList<string> Roles,
    string DeskId,
    int? DefaultQuoteExpiryMinutes);

public sealed record SecuritySearchResponse(
    string SecurityId,
    string JapaneseName,
    string BbgDisplay,
    string InternalCode,
    string Isin,
    string CategoryId,
    string CategoryName);

public sealed record ClientSearchResponse(string ClientId, string Code, string Name);
