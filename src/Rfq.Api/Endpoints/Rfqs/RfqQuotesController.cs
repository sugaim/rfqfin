using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqQuotes;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqQuotesController(
    IRfqQuoteQueries quotes,
    ConfirmQuotes confirm,
    WithdrawQuotes withdraw) : ControllerBase
{
    [HttpGet("{caseId:long}/quotes")]
    [EndpointName("GetRfqQuoteHistory")]
    public async Task<IReadOnlyList<QuoteHistoryResponse>> Get(
        long caseId, CancellationToken cancellationToken) =>
        [.. (await quotes.GetAsync(new CaseId(caseId), cancellationToken))
            .Select(RfqQuotesApiMapper.ToApi)];

    [HttpPost("withdraw-quotes")]
    [EndpointName("WithdrawQuotes")]
    public async Task<IReadOnlyList<CaseOperationResponse>> Withdraw(
        WithdrawQuotesRequest request, CancellationToken cancellationToken) =>
        [.. (await withdraw.ExecuteAsync(
            [.. request.Items.Select(item => new LifecycleItem(
                new CaseId(item.CaseId), new StateVersion(item.ExpectedCurrentVersion)))],
            cancellationToken)).Select(CaseOperationApiMapper.ToApi)];

    [HttpPost("confirm-quotes")]
    [EndpointName("ConfirmQuotes")]
    public async Task<IReadOnlyList<CaseOperationResponse>> Confirm(
        ConfirmQuotesRequest request, CancellationToken cancellationToken) =>
        [.. (await confirm.ExecuteAsync(
            [.. request.Items.Select(item => new ConfirmQuoteItem(
                new CaseId(item.CaseId),
                QuoteApiMapper.ToDomain(item.Expiry),
                new StateVersion(item.ExpectedCurrentVersion),
                new StateVersion(item.ExpectedWorkingQuoteVersion)))],
            cancellationToken)).Select(CaseOperationApiMapper.ToApi)];
}

public sealed record WithdrawQuoteItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record WithdrawQuotesRequest(
    [Required, MinLength(1)] IReadOnlyList<WithdrawQuoteItemRequest> Items);

public sealed record ConfirmQuoteItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Required] QuoteExpiryRequest Expiry,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record ConfirmQuotesRequest(
    [Required, MinLength(1)] IReadOnlyList<ConfirmQuoteItemRequest> Items);

public sealed record QuoteHistoryResponse(
    Guid QuoteId,
    Guid RevisionId,
    QuoteMode Mode,
    DateTimeOffset ConfirmedAt,
    DateTimeOffset? ExpiresAt,
    global::Rfq.Api.QuoteRequestReason RequestReason);

public static class RfqQuotesApiMapper
{
    public static QuoteHistoryResponse ToApi(QuoteHistoryItem value) => new(
        value.QuoteId.Value,
        value.RevisionId.Value,
        QuoteApiMapper.ToApi(value.Mode),
        value.ConfirmedAt,
        value.ExpiresAt,
        Enum.Parse<global::Rfq.Api.QuoteRequestReason>(value.RequestReason.ToString()));
}
