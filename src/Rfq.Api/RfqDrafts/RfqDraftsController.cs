using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqDrafts;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqDraftsController(
    CreateDraft createDraft,
    ConfirmNewRfq confirmNewRfq,
    UpdateInitialDraft updateDraft,
    ConfirmInitialDraft confirmDraft,
    DiscardInitialDraft discardDraft,
    CreateFromExisting createFromExisting,
    BulkConfirmInitialDrafts bulkConfirm,
    BulkDiscardInitialDrafts bulkDiscard) : ControllerBase
{
    [HttpPost("drafts")]
    public async Task<ActionResult<InitialRfqResponse>> Create(CreateDraftRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createDraft.ExecuteAsync(RfqDraftsApiMapper.ToCommand(request), cancellationToken);
        return Created($"/api/rfqs/{result.CaseId.Value}/draft", RfqDraftsApiMapper.ToApi(result));
    }

    [HttpPost("drafts/confirm")]
    public async Task<ActionResult<InitialRfqResponse>> CreateAndConfirm(CreateDraftRequest request,
        CancellationToken cancellationToken)
    {
        var result = await confirmNewRfq.ExecuteAsync(RfqDraftsApiMapper.ToCommand(request), cancellationToken);
        return Created($"/api/rfqs/{result.CaseId.Value}", RfqDraftsApiMapper.ToApi(result));
    }

    [HttpPut("{caseId:long}/draft")]
    public async Task<InitialRfqResponse> Update(long caseId, UpdateDraftRequest request,
        CancellationToken cancellationToken) => RfqDraftsApiMapper.ToApi(
            await updateDraft.ExecuteAsync(RfqDraftsApiMapper.ToCommand(caseId, request), cancellationToken));

    [HttpPost("{caseId:long}/draft/confirm")]
    public async Task<InitialRfqResponse> Confirm(long caseId, UpdateDraftRequest request,
        CancellationToken cancellationToken) => RfqDraftsApiMapper.ToApi(
            await confirmDraft.ExecuteAsync(RfqDraftsApiMapper.ToCommand(caseId, request), cancellationToken));

    [HttpPost("{caseId:long}/draft/discard")]
    public async Task<IActionResult> Discard(long caseId, VersionRequest request,
        CancellationToken cancellationToken)
    {
        await discardDraft.ExecuteAsync(new CaseId(caseId), new StateVersion(request.ExpectedVersion),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{caseId:long}/create-from-existing")]
    public async Task<ActionResult<InitialRfqResponse>> Copy(long caseId,
        CancellationToken cancellationToken)
    {
        var result = await createFromExisting.ExecuteAsync(new CaseId(caseId), cancellationToken);
        return Created($"/api/rfqs/{result.CaseId.Value}/draft", RfqDraftsApiMapper.ToApi(result));
    }

    [HttpPost("drafts/bulk-confirm")]
    public async Task<IReadOnlyList<BulkItemResponse>> BulkConfirm(
        BulkConfirmDraftRequest request, CancellationToken cancellationToken) =>
        (await bulkConfirm.ExecuteAsync(request.Items.Select(RfqDraftsApiMapper.ToCommand).ToArray(),
            cancellationToken)).Select(BulkApiMapper.ToApi).ToArray();

    [HttpPost("drafts/bulk-discard")]
    public async Task<IReadOnlyList<BulkItemResponse>> BulkDiscard(
        BulkDiscardDraftRequest request, CancellationToken cancellationToken) =>
        (await bulkDiscard.ExecuteAsync(request.Items.Select(item => new DiscardInitialDraftItem(
            new CaseId(item.CaseId), new StateVersion(item.ExpectedVersion))).ToArray(),
            cancellationToken)).Select(BulkApiMapper.ToApi).ToArray();
}

public sealed record CreateDraftRequest(
    [Required, MinLength(1)] string ClientId,
    [Required, MinLength(1)] string SecurityId,
    decimal? Notional,
    [Required] DateOnly? SettlementDate,
    [Required] DateOnly? StandardSettlementDate,
    [Required(AllowEmptyStrings = true)] string SalesAndTradingMessage,
    [Required, MinLength(1)] string AssignedTraderId);

public sealed record UpdateDraftRequest(
    decimal? Notional,
    [Required] DateOnly? SettlementDate,
    [Required] DateOnly? StandardSettlementDate,
    [Required(AllowEmptyStrings = true)] string SalesAndTradingMessage,
    [Required, MinLength(1)] string AssignedTraderId,
    [Range(1, long.MaxValue)] long ExpectedVersion);

public sealed record VersionRequest([Range(1, long.MaxValue)] long ExpectedVersion);
public sealed record ConfirmDraftItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    decimal? Notional,
    [Required] DateOnly? SettlementDate,
    [Required] DateOnly? StandardSettlementDate,
    [Required(AllowEmptyStrings = true)] string SalesAndTradingMessage,
    [Required, MinLength(1)] string AssignedTraderId,
    [Range(1, long.MaxValue)] long ExpectedVersion);
public sealed record BulkConfirmDraftRequest(
    [Required, MinLength(1)] IReadOnlyList<ConfirmDraftItemRequest> Items);
public sealed record DiscardDraftItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedVersion);
public sealed record BulkDiscardDraftRequest(
    [Required, MinLength(1)] IReadOnlyList<DiscardDraftItemRequest> Items);

public enum RfqStatusValue { Draft, Active, Presented, Cancelled, Hit, Away }
public enum RevisionStatusValue { Draft, Confirmed, Superseded, Discarded }
public enum QuoteStatusValue { Requested, Quoted }
public enum QuoteRequestReasonValue { Initial, Revised, Reopened, Expired, Withdrawn }

public sealed record InitialRfqResponse(
    long CaseId, Guid RevisionId, RfqStatusValue RfqStatus,
    RevisionStatusValue RevisionStatus, QuoteStatusValue? QuoteStatus,
    QuoteRequestReasonValue? QuoteRequestReason, string CategoryId,
    string ContactOwnerId, string AssignedTraderId, decimal? Notional,
    DateOnly SettlementDate, DateOnly StandardSettlementDate,
    string SalesAndTradingMessage, long Version, DateTimeOffset CreatedAt);

public static class RfqDraftsApiMapper
{
    public static CreateDraftCommand ToCommand(CreateDraftRequest request) => new(
        ClientId.Create(request.ClientId), SecurityId.Create(request.SecurityId), request.Notional,
        request.SettlementDate!.Value, request.StandardSettlementDate!.Value,
        request.SalesAndTradingMessage, UserId.Create(request.AssignedTraderId));

    public static UpdateInitialDraftCommand ToCommand(long caseId, UpdateDraftRequest request) => new(
        new CaseId(caseId), request.Notional, request.SettlementDate!.Value,
        request.StandardSettlementDate!.Value, request.SalesAndTradingMessage,
        UserId.Create(request.AssignedTraderId), new StateVersion(request.ExpectedVersion));

    public static UpdateInitialDraftCommand ToCommand(ConfirmDraftItemRequest request) => new(
        new CaseId(request.CaseId), request.Notional, request.SettlementDate!.Value,
        request.StandardSettlementDate!.Value, request.SalesAndTradingMessage,
        UserId.Create(request.AssignedTraderId), new StateVersion(request.ExpectedVersion));

    public static InitialRfqResponse ToApi(InitialRfqResult result) => new(
        result.CaseId.Value, result.RevisionId.Value, Map<RfqStatusValue>(result.RfqStatus),
        Map<RevisionStatusValue>(result.RevisionStatus), MapNullable<QuoteStatusValue>(result.QuoteStatus),
        MapNullable<QuoteRequestReasonValue>(result.QuoteRequestReason), result.CategoryId.Value,
        result.ContactOwnerId.Value, result.AssignedTraderId.Value, result.Notional,
        result.SettlementDate ?? throw new InvalidOperationException("Draft Settlement Date is missing."),
        result.StandardSettlementDate, result.SalesAndTradingMessage, result.Version.Value,
        result.CreatedAt);

    private static TApi Map<TApi>(Enum value) where TApi : struct, Enum =>
        Enum.Parse<TApi>(value.ToString(), false);
    private static TApi? MapNullable<TApi>(Enum? value) where TApi : struct, Enum =>
        value is null ? null : Map<TApi>(value);
}
