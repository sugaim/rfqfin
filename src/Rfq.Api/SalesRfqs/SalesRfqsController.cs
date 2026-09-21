using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.SalesRfqs;

[ApiController]
[Route("api/sales-rfqs")]
public sealed class SalesRfqsController(GetActiveSalesRfqs query) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SalesRfqResponse>> Get(CancellationToken token) =>
        (await query.ExecuteAsync(token)).Select(SalesRfqsApiMapper.ToApi).ToArray();
}

public enum RfqStatusValue { Draft, Active, Presented, Cancelled, Hit, Away }
public enum QuoteStatusValue { Requested, Quoted }
public enum QuoteRequestReasonValue { Initial, Revised, Reopened, Expired, Withdrawn }
public enum RevisionStatusValue { Draft, Confirmed, Superseded, Discarded }
public sealed record SalesRfqResponse(long CaseId, string ClientId, string ClientName,
    string SecurityId, string SecurityJapaneseName, string SecurityBbgDisplay,
    string CategoryId, RfqStatusValue RfqStatus, QuoteStatusValue? QuoteStatus,
    QuoteRequestReasonValue? QuoteRequestReason, Guid CurrentRevisionId,
    Guid? CurrentQuoteId, Guid? ClosedQuoteId, long CurrentVersion,
    RevisionStatusValue RevisionStatus, string ContactOwnerId, string AssignedTraderId,
    DateOnly? SettlementDate, DateOnly StandardSettlementDate, decimal? Notional,
    string SalesAndTradingMessage, string SalesMemo, long SalesMemoVersion,
    long Version, DateTimeOffset CreatedAt, Guid? DraftRevisionId, long? DraftVersion,
    DateOnly? DraftSettlementDate, decimal? DraftNotional, string? DraftSalesAndTradingMessage);

public static class SalesRfqsApiMapper
{
    public static SalesRfqResponse ToApi(SalesRfqListItem value) => new(
        value.CaseId.Value, value.ClientId.Value, value.ClientName, value.SecurityId.Value,
        value.SecurityJapaneseName, value.SecurityBbgDisplay, value.CategoryId.Value,
        Map<RfqStatusValue>(value.RfqStatus), MapNullable<QuoteStatusValue>(value.QuoteStatus),
        MapNullable<QuoteRequestReasonValue>(value.QuoteRequestReason),
        value.CurrentRevisionId.Value, value.CurrentQuoteId?.Value, value.ClosedQuoteId?.Value,
        value.CurrentVersion.Value, Map<RevisionStatusValue>(value.RevisionStatus),
        value.ContactOwnerId.Value, value.AssignedTraderId.Value, value.SettlementDate,
        value.StandardSettlementDate, value.Notional, value.SalesAndTradingMessage,
        value.SalesMemo, value.SalesMemoVersion.Value, value.Version.Value, value.CreatedAt,
        value.DraftRevisionId?.Value, value.DraftVersion?.Value, value.DraftSettlementDate,
        value.DraftNotional, value.DraftSalesAndTradingMessage);
    private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());
    private static T? MapNullable<T>(Enum? value) where T : struct, Enum =>
        value is null ? null : Map<T>(value);
}
