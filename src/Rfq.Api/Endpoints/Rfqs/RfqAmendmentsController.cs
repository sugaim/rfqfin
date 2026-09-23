using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqAmendments;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqAmendmentsController(
    StartAmendment start,
    SaveAmendment save,
    ConfirmAmendments confirm,
    DiscardAmendments discard) : ControllerBase
{
    [HttpPost("{caseId:long}/amendment/start")]
    [EndpointName("StartAmendment")]
    public async Task<AmendmentResponse> Start(
        long caseId,
        StartAmendmentRequest request,
        CancellationToken token) => AmendmentApiMapper.ToApi(await start.ExecuteAsync(
            new StartAmendmentCommand(
                new CaseId(caseId),
                new StateVersion(request.ExpectedCurrentVersion)),
            token));

    [HttpPut("{caseId:long}/amendment")]
    [EndpointName("SaveAmendment")]
    public async Task<AmendmentResponse> Save(
        long caseId,
        SaveAmendmentRequest request,
        CancellationToken token) => AmendmentApiMapper.ToApi(await save.ExecuteAsync(
            new SaveAmendmentCommand(
                new CaseId(caseId),
                request.Notional,
                request.SettlementDate,
                request.SalesAndTradingMessage,
                new StateVersion(request.ExpectedCurrentVersion),
                request.ExpectedDraftVersion is null ? null
                    : new StateVersion(request.ExpectedDraftVersion.Value)),
            token));

    [HttpPost("confirm-amendments")]
    [EndpointName("ConfirmAmendments")]
    public async Task<IReadOnlyList<CaseOperationResponse>> Confirm(
        ConfirmAmendmentsRequest request,
        CancellationToken token) => [.. (await confirm.ExecuteAsync(
            [.. request.Items.Select(AmendmentApiMapper.ToItem)], token))
            .Select(CaseOperationApiMapper.ToApi)];

    [HttpPost("discard-amendments")]
    [EndpointName("DiscardAmendments")]
    public async Task<IReadOnlyList<CaseOperationResponse>> Discard(
        DiscardAmendmentsRequest request,
        CancellationToken token) => [.. (await discard.ExecuteAsync(
            [.. request.Items.Select(AmendmentApiMapper.ToItem)], token))
            .Select(CaseOperationApiMapper.ToApi)];
}

public sealed record SaveAmendmentRequest(
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    long? ExpectedDraftVersion);

public sealed record StartAmendmentRequest(
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record AmendmentActionItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedDraftVersion);

public sealed record ConfirmAmendmentsRequest(
    [Required, MinLength(1)] IReadOnlyList<AmendmentActionItemRequest> Items);

public sealed record DiscardAmendmentsRequest(
    [Required, MinLength(1)] IReadOnlyList<AmendmentActionItemRequest> Items);

public sealed record AmendmentResponse(
    long CaseId,
    Guid CurrentRevisionId,
    Guid? DraftRevisionId,
    long CurrentVersion,
    long? DraftVersion,
    decimal? DraftNotional,
    DateOnly? DraftSettlementDate,
    string? DraftSalesAndTradingMessage,
    global::Rfq.Api.RfqStatus RfqStatus,
    global::Rfq.Api.QuoteStatus? QuoteStatus,
    global::Rfq.Api.QuoteRequestReason? QuoteRequestReason);

public static class AmendmentApiMapper
{
    public static AmendmentItem ToItem(AmendmentActionItemRequest value) => new(
        new CaseId(value.CaseId),
        new StateVersion(value.ExpectedCurrentVersion),
        new StateVersion(value.ExpectedDraftVersion));

    public static AmendmentResponse ToApi(AmendmentResult value) => new(
        value.CaseId.Value,
        value.CurrentRevisionId.Value,
        value.DraftRevisionId?.Value,
        value.CurrentVersion.Value,
        value.DraftVersion?.Value,
        value.DraftNotional,
        value.DraftSettlementDate,
        value.DraftSalesAndTradingMessage,
        Map<global::Rfq.Api.RfqStatus>(value.RfqStatus),
        MapNullable<global::Rfq.Api.QuoteStatus>(value.QuoteStatus),
        MapNullable<global::Rfq.Api.QuoteRequestReason>(value.QuoteRequestReason));

    private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());

    private static T? MapNullable<T>(Enum? value) where T : struct, Enum =>
        value is null ? null : Map<T>(value);
}
