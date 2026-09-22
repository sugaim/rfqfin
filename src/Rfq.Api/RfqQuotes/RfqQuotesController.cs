using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqQuotes;

[ApiController]
[Route("api/rfqs/{caseId:long}")]
public sealed class RfqQuotesController(
    ConfirmQuote confirm,
    WithdrawQuote withdraw,
    IRfqQuoteQueries quotes,
    BulkConfirmQuotes bulkConfirm,
    BulkWithdrawQuotes bulkWithdraw) : ControllerBase
{
    [HttpPost("quote/confirm")]
    public async Task<ConfirmQuoteResponse> Confirm(
        long caseId,
        ConfirmQuoteRequest request,
        CancellationToken cancellationToken) => RfqQuotesApiMapper.ToApi(
            await confirm.ExecuteAsync(
                new CaseId(caseId),
                QuoteApiMapper.ToDomain(request.Expiry),
                new StateVersion(request.ExpectedCurrentVersion),
                new StateVersion(request.ExpectedWorkingQuoteVersion),
                cancellationToken));

    [HttpPost("quote/withdraw")]
    public async Task<LifecycleResponse> Withdraw(
        long caseId,
        VersionRequest request,
        CancellationToken cancellationToken) => RfqQuotesApiMapper.ToApi(
            (await withdraw.ExecuteAsync(
                new CaseId(caseId),
                new StateVersion(request.ExpectedVersion),
                cancellationToken)).Rfq);

    [HttpGet("quotes")]
    public async Task<IReadOnlyList<QuoteHistoryResponse>> Get(
        long caseId,
        CancellationToken cancellationToken) => [.. (await quotes.GetAsync(
            new CaseId(caseId), cancellationToken)).Select(RfqQuotesApiMapper.ToApi)];

    [HttpPost("/api/rfqs/quotes/bulk-withdraw")]
    public async Task<IReadOnlyList<BulkItemResponse>> BulkWithdraw(
        BulkLifecycleRequest request, CancellationToken cancellationToken) =>
        [.. (await bulkWithdraw.ExecuteAsync(
            [.. request.Items.Select(item => new LifecycleItem(
                new CaseId(item.CaseId), new StateVersion(item.ExpectedCurrentVersion)))],
            cancellationToken)).Select(BulkApiMapper.ToApi)];

    [HttpPost("/api/rfqs/quotes/bulk-confirm")]
    public async Task<IReadOnlyList<BulkItemResponse>> BulkConfirm(
        BulkConfirmQuoteRequest request, CancellationToken cancellationToken) =>
        [.. (await bulkConfirm.ExecuteAsync(
            [.. request.Items.Select(item => new ConfirmQuoteItem(
                new CaseId(item.CaseId),
                QuoteApiMapper.ToDomain(item.Expiry),
                new StateVersion(item.ExpectedCurrentVersion),
                new StateVersion(item.ExpectedWorkingQuoteVersion)))],
            cancellationToken))
        .Select(BulkApiMapper.ToApi)];
}

public sealed record ConfirmQuoteRequest(
    [Required] QuoteExpiryRequest Expiry,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record VersionRequest([Range(1, long.MaxValue)] long ExpectedVersion);

public sealed record LifecycleItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record BulkLifecycleRequest(
    [Required, MinLength(1)] IReadOnlyList<LifecycleItemRequest> Items);

public sealed record ConfirmQuoteItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Required] QuoteExpiryRequest Expiry,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record BulkConfirmQuoteRequest(
    [Required, MinLength(1)] IReadOnlyList<ConfirmQuoteItemRequest> Items);

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

public sealed record ConfirmQuoteResponse(
    long CaseId,
    Guid QuoteId,
    Guid RevisionId,
    RfqStatusValue RfqStatus,
    QuoteStatusValue QuoteStatus,
    QuoteMode Mode,
    CalculatedQuoteResponse? Calculated,
    ManualQuoteResponse? Manual,
    DateTimeOffset ConfirmedAt,
    QuoteExpiryResponse Expiry,
    DateTimeOffset? ExpiresAt,
    long CurrentVersion);

public sealed record LifecycleResponse(
    long CaseId,
    RfqStatusValue RfqStatus,
    QuoteStatusValue? QuoteStatus,
    QuoteRequestReasonValue? QuoteRequestReason,
    long CurrentVersion);

public sealed record QuoteHistoryResponse(
    Guid QuoteId,
    Guid RevisionId,
    QuoteMode Mode,
    DateTimeOffset ConfirmedAt,
    DateTimeOffset? ExpiresAt,
    QuoteRequestReasonValue RequestReason);

public static class RfqQuotesApiMapper
{
    public static ConfirmQuoteResponse ToApi(ConfirmQuoteResult value) => new(
        value.CaseId.Value,
        value.QuoteId.Value,
        value.RevisionId.Value,
        Map<RfqStatusValue>(value.RfqStatus),
        Map<QuoteStatusValue>(value.QuoteStatus),
        QuoteApiMapper.ToApi(value.Mode),
        QuoteApiMapper.ToApi(value.Calculated),
        QuoteApiMapper.ToApi(value.Manual),
        value.ConfirmedAt,
        QuoteApiMapper.ToApi(value.Expiry),
        value.ExpiresAt,
        value.CurrentVersion.Value);

    public static LifecycleResponse ToApi(LifecycleResult value) => new(
        value.CaseId.Value,
        Map<RfqStatusValue>(value.RfqStatus),
        MapNullable<QuoteStatusValue>(value.QuoteStatus),
        MapNullable<QuoteRequestReasonValue>(value.QuoteRequestReason),
        value.CurrentVersion.Value);

    public static QuoteHistoryResponse ToApi(QuoteHistoryItem value) => new(
        value.QuoteId.Value,
        value.RevisionId.Value,
        QuoteApiMapper.ToApi(value.Mode),
        value.ConfirmedAt,
        value.ExpiresAt,
        Map<QuoteRequestReasonValue>(value.RequestReason));

    private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());

    private static T? MapNullable<T>(Enum? value) where T : struct, Enum =>
        value is null ? null : Map<T>(value);
}
