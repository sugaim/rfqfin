using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqAmendments;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqAmendmentsController(
    SaveAmendment save,
    ConfirmAmendment confirm,
    DiscardAmendment discard,
    BulkConfirmAmendments bulkConfirm,
    BulkDiscardAmendments bulkDiscard) : ControllerBase
{
    [HttpPut("{caseId:long}/amendment")]
    public async Task<AmendmentResponse> Save(
        long caseId,
        SaveAmendmentRequest request,
        CancellationToken token) => AmendmentApiMapper.ToApi(await save.ExecuteAsync(
            new SaveAmendmentCommand(
                new CaseId(caseId),
                request.Notional,
                request.SettlementDate,
                request.Message,
                new StateVersion(request.ExpectedCurrentVersion),
                request.ExpectedDraftVersion is null ? null
                    : new StateVersion(request.ExpectedDraftVersion.Value)),
            token));

    [HttpPost("{caseId:long}/amendment/confirm")]
    public async Task<AmendmentResponse> Confirm(
        long caseId,
        AmendmentActionRequest request,
        CancellationToken token) => AmendmentApiMapper.ToApi(await confirm.ExecuteAsync(
            AmendmentApiMapper.ToItem(caseId, request), token));

    [HttpPost("{caseId:long}/amendment/discard")]
    public async Task<AmendmentResponse> Discard(
        long caseId,
        AmendmentActionRequest request,
        CancellationToken token) => AmendmentApiMapper.ToApi(await discard.ExecuteAsync(
            AmendmentApiMapper.ToItem(caseId, request), token));

    [HttpPost("amendment/bulk-confirm")]
    public async Task<IReadOnlyList<BulkItemResponse>> BulkConfirm(
        BulkAmendmentRequest request,
        CancellationToken token) => [.. (await bulkConfirm.ExecuteAsync(
            [.. request.Items.Select(AmendmentApiMapper.ToItem)], token))
            .Select(BulkApiMapper.ToApi)];

    [HttpPost("amendment/bulk-discard")]
    public async Task<IReadOnlyList<BulkItemResponse>> BulkDiscard(
        BulkAmendmentRequest request,
        CancellationToken token) => [.. (await bulkDiscard.ExecuteAsync(
            [.. request.Items.Select(AmendmentApiMapper.ToItem)], token))
            .Select(BulkApiMapper.ToApi)];
}

public sealed record SaveAmendmentRequest(
    decimal? Notional,
    DateOnly? SettlementDate,
    string? Message,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    long? ExpectedDraftVersion);

public sealed record AmendmentActionRequest(
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedDraftVersion);

public sealed record AmendmentActionItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedDraftVersion);

public sealed record BulkAmendmentRequest(
    [Required, MinLength(1)] IReadOnlyList<AmendmentActionItemRequest> Items);

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

public sealed record AmendmentResponse(
    long CaseId,
    Guid CurrentRevisionId,
    Guid? DraftRevisionId,
    long CurrentVersion,
    long? DraftVersion,
    RfqStatusValue RfqStatus,
    QuoteStatusValue? QuoteStatus,
    QuoteRequestReasonValue? QuoteRequestReason);

public static class AmendmentApiMapper
{
    public static AmendmentItem ToItem(long caseId, AmendmentActionRequest value) => new(
        new CaseId(caseId),
        new StateVersion(value.ExpectedCurrentVersion),
        new StateVersion(value.ExpectedDraftVersion));

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
        Map<RfqStatusValue>(value.RfqStatus),
        MapNullable<QuoteStatusValue>(value.QuoteStatus),
        MapNullable<QuoteRequestReasonValue>(value.QuoteRequestReason));

    private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());

    private static T? MapNullable<T>(Enum? value) where T : struct, Enum =>
        value is null ? null : Map<T>(value);
}
