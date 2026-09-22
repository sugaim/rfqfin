using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Api.RfqQuotes;

namespace Rfq.Api.TraderRfqs;

[ApiController]
[Route("api/trader-rfqs")]
public sealed class TraderRfqsController(GetActiveTraderRfqs query) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<TraderRfqResponse>> Get(CancellationToken token) =>
        (await query.ExecuteAsync(token)).Select(TraderRfqsApiMapper.ToApi).ToArray();
}

public enum RfqStatusValue { Draft, Active, Presented, Cancelled, Hit, Away }
public enum QuoteStatusValue { Requested, Quoted }
public enum QuoteRequestReasonValue { Initial, Revised, Reopened, Expired, Withdrawn }
public sealed record TraderRfqResponse(long CaseId, string ClientId, string ClientName,
    string SecurityId, string SecurityJapaneseName, string SecurityBbgDisplay,
    string CategoryId, RfqStatusValue RfqStatus, QuoteStatusValue? QuoteStatus,
    QuoteRequestReasonValue? QuoteRequestReason, Guid CurrentRevisionId,
    Guid? CurrentQuoteId, Guid? ClosedQuoteId, DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ExpiresAt, Guid? QuoteSeedRevisionId, string ContactOwnerId,
    string AssignedTraderId, bool Owned, long CurrentVersion, DateOnly? SettlementDate,
    decimal? Notional, string SalesAndTradingMessage,
    QuoteMode WorkingQuoteMode, CalculatedQuoteResponse? Calculated,
    ManualQuoteResponse? Manual, long WorkingQuoteVersion, string TraderMemo,
    long TraderMemoVersion, DateTimeOffset CreatedAt, DateTimeOffset StateSince);

public static class TraderRfqsApiMapper
{
    public static TraderRfqResponse ToApi(TraderRfqListItem value) => new(
        value.CaseId.Value, value.ClientId.Value, value.ClientName, value.SecurityId.Value,
        value.SecurityJapaneseName, value.SecurityBbgDisplay, value.CategoryId.Value,
        Map<RfqStatusValue>(value.RfqStatus), MapNullable<QuoteStatusValue>(value.QuoteStatus),
        MapNullable<QuoteRequestReasonValue>(value.QuoteRequestReason),
        value.CurrentRevisionId.Value, value.CurrentQuoteId?.Value, value.ClosedQuoteId?.Value,
        value.ConfirmedAt, value.ExpiresAt, value.QuoteSeedRevisionId?.Value,
        value.ContactOwnerId.Value, value.AssignedTraderId.Value, value.Owned,
        value.CurrentVersion.Value, value.SettlementDate, value.Notional,
        value.SalesAndTradingMessage,
        QuoteApiMapper.ToApi(value.WorkingQuoteMode), QuoteApiMapper.ToApi(value.Calculated),
        QuoteApiMapper.ToApi(value.Manual), value.WorkingQuoteVersion.Value,
        value.TraderMemo, value.TraderMemoVersion.Value, value.CreatedAt, value.StateSince);
    private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());
    private static T? MapNullable<T>(Enum? value) where T : struct, Enum =>
        value is null ? null : Map<T>(value);
}
