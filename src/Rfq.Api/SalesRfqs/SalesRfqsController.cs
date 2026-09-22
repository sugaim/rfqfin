using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.SalesRfqs;

[ApiController]
[Route("api/sales-rfqs")]
public sealed class SalesRfqsController(
    GetActiveSalesRfqs query,
    ISalesRecentRevisionQueries recentRevisions,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SalesRfqResponse>> Get(CancellationToken token) =>
        [.. (await query.ExecuteAsync(token)).Select(SalesRfqsApiMapper.ToApi)];

    [HttpGet("recent-revisions")]
    public async Task<IReadOnlyList<SalesRecentRevisionResponse>> GetRecentRevisions(
        [FromQuery, Range(1, 100)] int limit = 50,
        CancellationToken token = default) =>
        [.. (await recentRevisions.GetAsync(currentUser.User.UserId, limit, token)).Select(SalesRfqsApiMapper.ToApi)];
}

public enum RfqStatusValue
{
    Draft,
    Active,
    Presented,
    Cancelled,
    Hit,
    Away
}

public enum QuoteStatusValue
{
    Requested,
    Quoted
}

public enum QuoteRequestReasonValue
{
    Initial,
    Revised,
    Reopened,
    Expired,
    Withdrawn
}

public enum RevisionStatusValue
{
    Draft,
    Confirmed,
    Superseded,
    Discarded
}

public enum WorkingQuoteModeValue
{
    Calculated,
    Manual
}

public enum SalesRecentRevisionKindValue
{
    Rfq,
    Quote
}

public enum SalesRecentRevisionFieldValue
{
    Notional,
    Settlement,
    Message,
    Price,
    Yield,
    Simple,
    GSpread,
}

public sealed record SalesConfirmedQuoteSummaryResponse(
    Guid QuoteId,
    WorkingQuoteModeValue Mode,
    decimal? Price,
    decimal? BbgYield,
    decimal? FinalSimpleYield,
    decimal? GSpread,
    DateTimeOffset ConfirmedAt);

public sealed record SalesRfqResponse(
    long CaseId,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityJapaneseName,
    string SecurityBbgDisplay,
    string CategoryId,
    RfqStatusValue RfqStatus,
    QuoteStatusValue? QuoteStatus,
    QuoteRequestReasonValue? QuoteRequestReason,
    Guid CurrentRevisionId,
    Guid? CurrentQuoteId,
    Guid? ClosedQuoteId,
    long CurrentVersion,
    RevisionStatusValue RevisionStatus,
    string? SalesId,
    string ContactOwnerId,
    string AssignedTraderId,
    DateOnly? SettlementDate,
    DateOnly StandardSettlementDate,
    decimal? Notional,
    string SalesAndTradingMessage,
    string SalesMemo,
    long SalesMemoVersion,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset StateSince,
    SalesConfirmedQuoteSummaryResponse? ConfirmedQuote,
    Guid? DraftRevisionId,
    long? DraftVersion,
    DateOnly? DraftSettlementDate,
    decimal? DraftNotional,
    string? DraftSalesAndTradingMessage);

public sealed record SalesRecentRevisionChangeResponse(
    SalesRecentRevisionFieldValue Field, string? Before, string? After);

public sealed record SalesRecentRevisionResponse(
    SalesRecentRevisionKindValue Kind,
    DateTimeOffset OccurredAt,
    long CaseId,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityName,
    IReadOnlyList<SalesRecentRevisionChangeResponse> Changes);

public static class SalesRfqsApiMapper
{
    public static SalesRfqResponse ToApi(SalesRfqListItem value) => new(
        value.CaseId.Value,
        value.ClientId.Value,
        value.ClientName,
        value.SecurityId.Value,
        value.SecurityJapaneseName,
        value.SecurityBbgDisplay,
        value.CategoryId.Value,
        Map<RfqStatusValue>(value.RfqStatus),
        MapNullable<QuoteStatusValue>(value.QuoteStatus),
        MapNullable<QuoteRequestReasonValue>(value.QuoteRequestReason),
        value.CurrentRevisionId.Value,
        value.CurrentQuoteId?.Value,
        value.ClosedQuoteId?.Value,
        value.CurrentVersion.Value,
        Map<RevisionStatusValue>(value.RevisionStatus),
        value.SalesId?.Value,
        value.ContactOwnerId.Value,
        value.AssignedTraderId.Value,
        value.SettlementDate,
        value.StandardSettlementDate,
        value.Notional,
        value.SalesAndTradingMessage,
        value.SalesMemo,
        value.SalesMemoVersion.Value,
        value.Version.Value,
        value.CreatedAt,
        value.StateSince,
        value.ConfirmedQuote is null ? null : new(
            value.ConfirmedQuote.QuoteId.Value,
            Map<WorkingQuoteModeValue>(value.ConfirmedQuote.Mode),
            value.ConfirmedQuote.Price,
            value.ConfirmedQuote.BbgYield,
            value.ConfirmedQuote.FinalSimpleYield,
            value.ConfirmedQuote.GSpread,
            value.ConfirmedQuote.ConfirmedAt),
        value.DraftRevisionId?.Value,
        value.DraftVersion?.Value,
        value.DraftSettlementDate,
        value.DraftNotional,
        value.DraftSalesAndTradingMessage);

    public static SalesRecentRevisionResponse ToApi(SalesRecentRevisionItem value) => new(
        Map<SalesRecentRevisionKindValue>(value.Kind),
        value.OccurredAt,
        value.CaseId.Value,
        value.ClientId.Value,
        value.ClientName,
        value.SecurityId.Value,
        value.SecurityName,
        [.. value.Changes.Select(change => new SalesRecentRevisionChangeResponse(
            Map<SalesRecentRevisionFieldValue>(change.Field), change.Before, change.After))]);

    private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());

    private static T? MapNullable<T>(Enum? value) where T : struct, Enum =>
        value is null ? null : Map<T>(value);
}
