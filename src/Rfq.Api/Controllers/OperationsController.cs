using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/operations")]
public sealed class OperationsController(
    IPastRfqQueries pastRfqs,
    IRfqHistoryQueries histories,
    IEodQueries eod,
    IGridConfigStore gridConfigs,
    ScratchPricer pricer) : ControllerBase
{
    [HttpGet("past-rfqs")]
    public async Task<PastRfqResponse> Search([FromQuery] PastRfqSearchRequest search,
        CancellationToken cancellationToken)
    {
        var result = await pastRfqs.SearchAsync(search.ToApplication(), cancellationToken);
        return new PastRfqResponse(result.Items.Select(PastRfqItemResponse.From).ToArray(),
            result.RequiresNarrowing);
    }

    [HttpGet("rfqs/{caseId:long}/revisions")]
    public async Task<IReadOnlyList<RevisionHistoryResponse>> Revisions(long caseId,
        CancellationToken cancellationToken) => (await histories.GetRevisionHistoryAsync(
            new CaseId(caseId), cancellationToken)).Select(RevisionHistoryResponse.From).ToArray();

    [HttpGet("rfqs/{caseId:long}/quotes")]
    public async Task<IReadOnlyList<QuoteHistoryResponse>> Quotes(long caseId,
        CancellationToken cancellationToken) => (await histories.GetQuoteHistoryAsync(
            new CaseId(caseId), cancellationToken)).Select(QuoteHistoryResponse.From).ToArray();

    [HttpGet("eod")]
    public async Task<IReadOnlyList<EodSummaryResponse>> Eod([FromQuery] DateOnly date,
        CancellationToken cancellationToken) => (await eod.GetEodAsync(date, cancellationToken))
            .Select(item => new EodSummaryResponse(
                item.ContactOwnerId.Value, item.Open, item.Hit, item.Away)).ToArray();

    [HttpGet("grid-config/{screenId}/{configKey}")]
    public async Task<ActionResult<GridConfig>> GetGridConfig(string screenId, string configKey,
        CancellationToken cancellationToken)
    {
        var value = await gridConfigs.GetAsync(screenId, configKey, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPut("grid-config/{screenId}/{configKey}")]
    public Task<GridConfig> SaveGridConfig(string screenId, string configKey,
        GridConfigRequest request, CancellationToken cancellationToken) =>
        gridConfigs.SaveAsync(screenId, configKey, request.Version,
            request.ConfigJson, cancellationToken);

    [HttpPost("pricer")]
    public Task<CalculatedQuotePayload> Price(ScratchPriceApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CalculationDriver>(request.Driver, false, out var driver))
        {
            throw new ArgumentException($"Unknown calculation driver '{request.Driver}'.");
        }

        return pricer.ExecuteAsync(
            new ScratchPriceRequest(SecurityId.Create(request.SecurityId), request.SettlementDate,
                driver, request.Value, request.SimpleYieldSlide), cancellationToken);
    }
}

public sealed record GridConfigRequest(int Version, string ConfigJson);

public sealed record ScratchPriceApiRequest(
    string SecurityId,
    DateOnly SettlementDate,
    string Driver,
    decimal Value,
    decimal SimpleYieldSlide);

public sealed record PastRfqSearchRequest(
    DateOnly? From = null,
    DateOnly? To = null,
    string? ClientId = null,
    string? SecurityId = null,
    string? CategoryId = null,
    string? ContactOwnerId = null,
    string? SalesId = null,
    string? AssignedTraderId = null,
    RfqStatus? Status = null,
    long? CaseId = null)
{
    public PastRfqSearch ToApplication() => new(
        From,
        To,
        ClientId is null ? null : Rfq.Domain.ClientId.Create(ClientId),
        SecurityId is null ? null : Rfq.Domain.SecurityId.Create(SecurityId),
        CategoryId is null ? null : Rfq.Domain.CategoryId.Create(CategoryId),
        ContactOwnerId is null ? null : UserId.Create(ContactOwnerId),
        SalesId is null ? null : UserId.Create(SalesId),
        AssignedTraderId is null ? null : UserId.Create(AssignedTraderId),
        Status,
        CaseId is null ? null : new Rfq.Domain.CaseId(CaseId.Value));
}

public sealed record PastRfqResponse(
    IReadOnlyList<PastRfqItemResponse> Items,
    bool RequiresNarrowing);

public sealed record PastRfqItemResponse(
    long CaseId,
    DateTimeOffset CreatedAt,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityName,
    string CategoryId,
    string Status,
    string? QuoteStatus,
    string ContactOwnerId,
    string? SalesId,
    string AssignedTraderId,
    decimal? Notional,
    DateOnly? SettlementDate)
{
    public static PastRfqItemResponse From(PastRfqItem item) => new(
        item.CaseId.Value, item.CreatedAt, item.ClientId.Value, item.ClientName,
        item.SecurityId.Value, item.SecurityName, item.CategoryId.Value,
        item.Status.ToString(), item.QuoteStatus?.ToString(), item.ContactOwnerId.Value,
        item.SalesId?.Value, item.AssignedTraderId.Value, item.Notional, item.SettlementDate);
}

public sealed record RevisionHistoryResponse(
    Guid RevisionId,
    string Status,
    decimal? Notional,
    DateOnly? SettlementDate,
    string Message,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    Guid? CopiedFromRevisionId,
    Guid? QuoteSeedRevisionId)
{
    public static RevisionHistoryResponse From(RevisionHistoryItem item) => new(
        item.RevisionId.Value, item.Status.ToString(), item.Notional, item.SettlementDate,
        item.Message, item.Version.Value, item.CreatedAt, item.ConfirmedAt,
        item.CopiedFromRevisionId?.Value, item.QuoteSeedRevisionId?.Value);
}

public sealed record QuoteHistoryResponse(
    Guid QuoteId,
    Guid RevisionId,
    string Mode,
    DateTimeOffset ConfirmedAt,
    DateTimeOffset? ExpiresAt,
    string RequestReason)
{
    public static QuoteHistoryResponse From(QuoteHistoryItem item) => new(
        item.QuoteId.Value, item.RevisionId.Value, item.Mode.ToString(), item.ConfirmedAt,
        item.ExpiresAt, item.RequestReason.ToString());
}

public sealed record EodSummaryResponse(string ContactOwnerId, int Open, int Hit, int Away);
