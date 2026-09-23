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
    CreateFromExisting createFromExisting,
    ConfirmInitialDrafts confirmDrafts,
    DiscardInitialDrafts discardDrafts) : ControllerBase
{
    [HttpPost("drafts")]
    [EndpointName("CreateDraft")]
    public async Task<ActionResult<InitialRfqResponse>> Create(
        CreateDraftRequest request,
        CancellationToken cancellationToken)
    {
        InitialRfqResult result = await createDraft.ExecuteAsync(RfqDraftsApiMapper.ToCommand(request), cancellationToken);
        return Created($"/api/rfqs/{result.CaseId.Value}/draft", RfqDraftsApiMapper.ToApi(result));
    }

    [HttpPost("drafts/confirm")]
    [EndpointName("ConfirmNewRfq")]
    public async Task<ActionResult<InitialRfqResponse>> CreateAndConfirm(
        CreateDraftRequest request,
        CancellationToken cancellationToken)
    {
        InitialRfqResult result = await confirmNewRfq.ExecuteAsync(RfqDraftsApiMapper.ToCommand(request), cancellationToken);
        return Created($"/api/rfqs/{result.CaseId.Value}", RfqDraftsApiMapper.ToApi(result));
    }

    [HttpPut("{caseId:long}/draft")]
    [EndpointName("UpdateInitialDraft")]
    public async Task<InitialRfqResponse> Update(
        long caseId,
        UpdateDraftRequest request,
        CancellationToken cancellationToken) => RfqDraftsApiMapper.ToApi(
            await updateDraft.ExecuteAsync(RfqDraftsApiMapper.ToCommand(caseId, request), cancellationToken));

    [HttpPost("{caseId:long}/create-from-existing")]
    [EndpointName("CreateFromExisting")]
    public async Task<ActionResult<InitialRfqResponse>> Copy(
        long caseId,
        CancellationToken cancellationToken)
    {
        InitialRfqResult result = await createFromExisting.ExecuteAsync(new CaseId(caseId), cancellationToken);
        return Created($"/api/rfqs/{result.CaseId.Value}/draft", RfqDraftsApiMapper.ToApi(result));
    }

    [HttpPost("confirm-initial-drafts")]
    [EndpointName("ConfirmInitialDrafts")]
    public async Task<IReadOnlyList<CaseOperationResponse>> Confirm(
        ConfirmInitialDraftsRequest request, CancellationToken cancellationToken) =>
        [.. (await confirmDrafts.ExecuteAsync(
            [.. request.Items.Select(RfqDraftsApiMapper.ToCommand)],
            cancellationToken)).Select(CaseOperationApiMapper.ToApi)];

    [HttpPost("discard-initial-drafts")]
    [EndpointName("DiscardInitialDrafts")]
    public async Task<IReadOnlyList<CaseOperationResponse>> Discard(
        DiscardInitialDraftsRequest request, CancellationToken cancellationToken) =>
        [.. (await discardDrafts.ExecuteAsync(
            [.. request.Items.Select(item => new DiscardInitialDraftItem(
                new CaseId(item.CaseId), new StateVersion(item.ExpectedCurrentVersion)))],
            cancellationToken)).Select(CaseOperationApiMapper.ToApi)];
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
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record ConfirmDraftItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    decimal? Notional,
    [Required] DateOnly? SettlementDate,
    [Required] DateOnly? StandardSettlementDate,
    [Required(AllowEmptyStrings = true)] string SalesAndTradingMessage,
    [Required, MinLength(1)] string AssignedTraderId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record ConfirmInitialDraftsRequest(
    [Required, MinLength(1)] IReadOnlyList<ConfirmDraftItemRequest> Items);

public sealed record DiscardDraftItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record DiscardInitialDraftsRequest(
    [Required, MinLength(1)] IReadOnlyList<DiscardDraftItemRequest> Items);

public sealed record InitialRfqResponse(
    long CaseId,
    Guid RevisionId,
    global::Rfq.Api.RfqStatus RfqStatus,
    global::Rfq.Api.RevisionStatus RevisionStatus,
    global::Rfq.Api.QuoteStatus? QuoteStatus,
    global::Rfq.Api.QuoteRequestReason? QuoteRequestReason,
    string CategoryId,
    string ContactOwnerId,
    string AssignedTraderId,
    decimal? Notional,
    DateOnly SettlementDate,
    DateOnly StandardSettlementDate,
    string SalesAndTradingMessage,
    long Version,
    DateTimeOffset CreatedAt);

public static class RfqDraftsApiMapper
{
    public static CreateDraftCommand ToCommand(CreateDraftRequest request) => new(
        ClientId.Create(request.ClientId),
        SecurityId.Create(request.SecurityId),
        request.Notional,
        request.SettlementDate!.Value,
        request.StandardSettlementDate!.Value,
        request.SalesAndTradingMessage,
        UserId.Create(request.AssignedTraderId));

    public static UpdateInitialDraftCommand ToCommand(long caseId, UpdateDraftRequest request) => new(
        new CaseId(caseId),
        request.Notional,
        request.SettlementDate!.Value,
        request.StandardSettlementDate!.Value,
        request.SalesAndTradingMessage,
        UserId.Create(request.AssignedTraderId),
        new StateVersion(request.ExpectedCurrentVersion));

    public static UpdateInitialDraftCommand ToCommand(ConfirmDraftItemRequest request) => new(
        new CaseId(request.CaseId),
        request.Notional,
        request.SettlementDate!.Value,
        request.StandardSettlementDate!.Value,
        request.SalesAndTradingMessage,
        UserId.Create(request.AssignedTraderId),
        new StateVersion(request.ExpectedCurrentVersion));

    public static InitialRfqResponse ToApi(InitialRfqResult result) => new(
        result.CaseId.Value,
        result.RevisionId.Value,
        Map<global::Rfq.Api.RfqStatus>(result.RfqStatus),
        Map<global::Rfq.Api.RevisionStatus>(result.RevisionStatus),
        MapNullable<global::Rfq.Api.QuoteStatus>(result.QuoteStatus),
        MapNullable<global::Rfq.Api.QuoteRequestReason>(result.QuoteRequestReason),
        result.CategoryId.Value,
        result.ContactOwnerId.Value,
        result.AssignedTraderId.Value,
        result.Notional,
        result.SettlementDate ?? throw new InvalidOperationException("Draft Settlement Date is missing."),
        result.StandardSettlementDate,
        result.SalesAndTradingMessage,
        result.Version.Value,
        result.CreatedAt);

    private static TApi Map<TApi>(Enum value) where TApi : struct, Enum =>
        Enum.Parse<TApi>(value.ToString(), false);

    private static TApi? MapNullable<TApi>(Enum? value) where TApi : struct, Enum =>
        value is null ? null : Map<TApi>(value);
}
