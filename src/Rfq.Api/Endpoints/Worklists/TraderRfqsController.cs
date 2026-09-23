using Microsoft.AspNetCore.Mvc;
using Rfq.Api.RfqQuotes;
using Rfq.Application;

namespace Rfq.Api.TraderRfqs;

[ApiController]
[Route("api/worklists/trader")]
public sealed class TraderRfqsController(GetActiveTraderRfqs query) : ControllerBase
{
    [HttpGet]
    [EndpointName("GetActiveTraderRfqs")]
    public async Task<IReadOnlyList<TraderRfqResponse>> Get(CancellationToken token) =>
        [.. (await query.ExecuteAsync(token)).Select(TraderRfqsApiMapper.ToApi)];
}

public sealed record TraderRfqResponse(
    long CaseId,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityJapaneseName,
    string SecurityBbgDisplay,
    string CategoryId,
    global::Rfq.Api.RfqStatus RfqStatus,
    global::Rfq.Api.QuoteStatus? QuoteStatus,
    global::Rfq.Api.QuoteRequestReason? QuoteRequestReason,
    Guid CurrentRevisionId,
    Guid? CurrentQuoteId,
    Guid? ClosedQuoteId,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ExpiresAt,
    Guid? QuoteSeedRevisionId,
    string ContactOwnerId,
    string AssignedTraderId,
    bool Owned,
    long CurrentVersion,
    DateOnly? SettlementDate,
    decimal? Notional,
    string SalesAndTradingMessage,
    QuoteMode WorkingQuoteMode,
    CalculatedQuoteResponse? Calculated,
    ManualQuoteResponse? Manual,
    long WorkingQuoteVersion,
    string TraderMemo,
    long TraderMemoVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset StateSince);

public static class TraderRfqsApiMapper
{
    public static TraderRfqResponse ToApi(TraderRfqListItem value) => new(
        value.CaseId.Value,
        value.ClientId.Value,
        value.ClientName,
        value.SecurityId.Value,
        value.SecurityJapaneseName,
        value.SecurityBbgDisplay,
        value.CategoryId.Value,
        Map<global::Rfq.Api.RfqStatus>(value.RfqStatus),
        MapNullable<global::Rfq.Api.QuoteStatus>(value.QuoteStatus),
        MapNullable<global::Rfq.Api.QuoteRequestReason>(value.QuoteRequestReason),
        value.CurrentRevisionId.Value,
        value.CurrentQuoteId?.Value,
        value.ClosedQuoteId?.Value,
        value.ConfirmedAt,
        value.ExpiresAt,
        value.QuoteSeedRevisionId?.Value,
        value.ContactOwnerId.Value,
        value.AssignedTraderId.Value,
        value.Owned,
        value.CurrentVersion.Value,
        value.SettlementDate,
        value.Notional,
        value.SalesAndTradingMessage,
        QuoteApiMapper.ToApi(value.WorkingQuoteMode),
        QuoteApiMapper.ToApi(value.Calculated),
        QuoteApiMapper.ToApi(value.Manual),
        value.WorkingQuoteVersion.Value,
        value.TraderMemo,
        value.TraderMemoVersion.Value,
        value.CreatedAt,
        value.StateSince);

    private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());

    private static T? MapNullable<T>(Enum? value) where T : struct, Enum =>
        value is null ? null : Map<T>(value);
}
