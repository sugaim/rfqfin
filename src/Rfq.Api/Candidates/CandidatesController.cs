using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Candidates;

[ApiController]
public sealed class CandidatesController(
    IUserCandidateQueries users,
    IClientSearch clients,
    ISecuritySearch securities,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("api/assignable-traders")]
    public async Task<IReadOnlyList<UserCandidateResponse>> AssignableTraders(
        CancellationToken cancellationToken) => [.. (await users.GetAssignableTradersAsync(
            currentUser.User.DeskId, cancellationToken)).Select(CandidateApiMapper.ToApi)];

    [HttpGet("api/contact-owner-candidates")]
    public async Task<IReadOnlyList<UserCandidateResponse>> ContactOwners(
        CancellationToken cancellationToken) => [.. (await users.GetContactOwnersAsync(
            currentUser.User.DeskId, cancellationToken)).Select(CandidateApiMapper.ToApi)];

    [HttpGet("api/rfqs/candidates/clients")]
    public async Task<IReadOnlyList<ClientCandidateResponse>> Clients(
        [FromQuery, Required] string q, CancellationToken cancellationToken) =>
        [.. (await clients.SearchAsync(q, cancellationToken)).Select(CandidateApiMapper.ToApi)];

    [HttpGet("api/rfqs/candidates/securities")]
    public async Task<IReadOnlyList<SecurityCandidateResponse>> Securities(
        [FromQuery, Required] string q, CancellationToken cancellationToken) =>
        [.. (await securities.SearchAsync(q, cancellationToken)).Select(CandidateApiMapper.ToApi)];
}

public sealed record UserCandidateResponse(string UserId, string Name);
public sealed record ClientCandidateResponse(string ClientId, string Code, string Name);

public sealed record SecurityCandidateResponse(
    string SecurityId,
    string JapaneseName,
    string BbgDisplay,
    string InternalCode,
    string Isin,
    string CategoryId,
    string CategoryName);

public static class CandidateApiMapper
{
    public static UserCandidateResponse ToApi(UserCandidate value) => new(value.UserId.Value, value.Name);

    public static ClientCandidateResponse ToApi(ClientSearchResult value) =>
        new(value.ClientId.Value, value.Code, value.Name);

    public static SecurityCandidateResponse ToApi(SecuritySearchResult value) => new(
        value.SecurityId.Value,
        value.JapaneseName,
        value.BbgDisplay,
        value.InternalCode,
        value.Isin,
        value.CategoryId.Value,
        value.CategoryName);
}
