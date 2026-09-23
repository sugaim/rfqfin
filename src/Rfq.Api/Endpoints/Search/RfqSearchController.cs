using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqSearch;

[ApiController]
[Route("api/search/rfqs")]
public sealed class RfqSearchController(IRfqSearchQueries queries) : ControllerBase
{
    [HttpGet]
    [EndpointName("SearchRfqs")]
    public async Task<RfqSearchResponse> Get(
        [FromQuery] RfqSearchRequest request,
        CancellationToken token) => RfqSearchApiMapper.ToApi(await queries.SearchAsync(
            RfqSearchApiMapper.ToQuery(request), token));
}

public sealed record RfqSearchRequest(
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    string? ClientId = null,
    string? SecurityId = null,
    string? CategoryId = null,
    string? ContactOwnerId = null,
    string? SalesId = null,
    string? AssignedTraderId = null,
    global::Rfq.Api.RfqStatus? Status = null,
    long? CaseId = null);

public sealed record RfqSearchResponse(
    IReadOnlyList<RfqSearchItemResponse> Items,
    bool RequiresNarrowing);

public sealed record RfqSearchItemResponse(
    long CaseId,
    DateTimeOffset CreatedAt,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityName,
    string CategoryId,
    global::Rfq.Api.RfqStatus Status,
    global::Rfq.Api.QuoteStatus? QuoteStatus,
    string ContactOwnerId,
    string? SalesId,
    string AssignedTraderId,
    decimal? Notional,
    DateOnly? SettlementDate,
    decimal? Price,
    decimal? FinalSimpleYield,
    decimal? Ysc);

public static class RfqSearchApiMapper
{
    public static Application.RfqSearch ToQuery(RfqSearchRequest value) => new(
        value.CreatedFrom,
        value.CreatedTo,
        value.ClientId is null ? null : Domain.ClientId.Create(value.ClientId),
        value.SecurityId is null ? null : Domain.SecurityId.Create(value.SecurityId),
        value.CategoryId is null ? null : Domain.CategoryId.Create(value.CategoryId),
        value.ContactOwnerId is null ? null : UserId.Create(value.ContactOwnerId),
        value.SalesId is null ? null : UserId.Create(value.SalesId),
        value.AssignedTraderId is null ? null : UserId.Create(value.AssignedTraderId),
        value.Status is null ? null : Enum.Parse<Domain.RfqStatus>(value.Status.Value.ToString()),
        value.CaseId is null ? null : new CaseId(value.CaseId.Value));

    public static RfqSearchResponse ToApi(RfqSearchResult value) => new(
        [.. value.Items.Select(ToApi)], value.RequiresNarrowing);

    private static RfqSearchItemResponse ToApi(RfqSearchItem value) => new(
        value.CaseId.Value,
        value.CreatedAt,
        value.ClientId.Value,
        value.ClientName,
        value.SecurityId.Value,
        value.SecurityName,
        value.CategoryId.Value,
        Enum.Parse<global::Rfq.Api.RfqStatus>(value.Status.ToString()),
        value.QuoteStatus is null ? null : Enum.Parse<global::Rfq.Api.QuoteStatus>(value.QuoteStatus.Value.ToString()),
        value.ContactOwnerId.Value,
        value.SalesId?.Value,
        value.AssignedTraderId.Value,
        value.Notional,
        value.SettlementDate,
        value.Price,
        value.FinalSimpleYield,
        value.Ysc);
}
