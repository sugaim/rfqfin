using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Api.RfqLifecycle;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.PostProcess;

[ApiController]
[Route("api/post-process")]
public sealed class PostProcessController(
    GetPostProcessWorklist getWorklist,
    CommitPostProcessChanges commit) : ControllerBase
{
    [HttpGet]
    [EndpointName("GetPostProcess")]
    public async Task<IReadOnlyList<PostProcessItemResponse>> Get(
        PostProcessPresetValue preset = PostProcessPresetValue.Today,
        PostProcessScopeValue scope = PostProcessScopeValue.Mine,
        CancellationToken cancellationToken = default) =>
        [.. (await getWorklist.ExecuteAsync(
            Map<PostProcessPreset>(preset),
            Map<PostProcessScope>(scope),
            cancellationToken)).Select(ToApi)];

    [HttpPost("commit")]
    [EndpointName("CommitPostProcessChanges")]
    public async Task<IReadOnlyList<CaseOperationResponse>> Commit(
        PostProcessCommitRequest request,
        CancellationToken cancellationToken) =>
        [.. (await commit.ExecuteAsync(
            [.. request.Items.Select(ToApplication)],
            cancellationToken)).Select(CaseOperationApiMapper.ToApi)];

    private static PostProcessCommitItem ToApplication(
        PostProcessCommitItemRequest request) => new(
            new CaseId(request.CaseId),
            new StateVersion(request.ExpectedCurrentVersion),
            request.LifecycleChange is null
                ? null
                : new PostProcessLifecycleChange(
                    Map<PostProcessLifecycleChangeKind>(request.LifecycleChange.Type),
                    request.LifecycleChange.CorrectionReason),
            request.MemoChange is null
                ? null
                : new PostProcessMemoChange(
                    new StateVersion(request.MemoChange.ExpectedMemoVersion),
                    request.MemoChange.Value));

    private static PostProcessItemResponse ToApi(PostProcessWorklistItem value) => new(
        value.CaseId.Value,
        value.CreatedAt,
        value.CreatedBusinessDate,
        value.ClientId.Value,
        value.ClientName,
        value.SecurityId.Value,
        value.SecurityName,
        value.SecurityBbgDisplay,
        value.Notional,
        value.SettlementDate,
        value.ContactOwnerId.Value,
        value.SalesId?.Value,
        value.AssignedTraderId.Value,
        Map<global::Rfq.Api.RfqStatus>(value.RfqStatus),
        value.CurrentVersion.Value,
        value.SalesAndTradingMessage,
        value.MyMemo,
        value.MyMemoVersion.Value,
        value.Price,
        value.FinalSimpleYield,
        value.Yield,
        value.Ysc,
        value.GSpread,
        value.ClosedBusinessDate,
        value.LastCorrectionReason,
        value.LastChangedBy?.Value,
        value.LastChangedAt);

    private static T Map<T>(Enum value) where T : struct, Enum =>
        Enum.Parse<T>(value.ToString());
}

public enum PostProcessPresetValue
{
    Today,
    Unclosed,
}

public enum PostProcessScopeValue
{
    Mine,
    AllPermitted,
}

public enum PostProcessLifecycleChangeValue
{
    Hit,
    Away,
    Cancel,
    CorrectToHit,
    CorrectToAway,
}

public sealed record PostProcessItemResponse(
    long CaseId,
    DateTimeOffset CreatedAt,
    DateOnly CreatedBusinessDate,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityName,
    string SecurityBbgDisplay,
    decimal? Notional,
    DateOnly? SettlementDate,
    string ContactOwnerId,
    string? SalesId,
    string AssignedTraderId,
    global::Rfq.Api.RfqStatus RfqStatus,
    long CurrentVersion,
    string SalesAndTradingMessage,
    string MyMemo,
    long MyMemoVersion,
    decimal? Price,
    decimal? FinalSimpleYield,
    decimal? Yield,
    decimal? Ysc,
    decimal? GSpread,
    DateOnly? ClosedBusinessDate,
    string? LastCorrectionReason,
    string? LastChangedBy,
    DateTimeOffset? LastChangedAt);

public sealed record PostProcessLifecycleChangeRequest(
    PostProcessLifecycleChangeValue Type,
    string? CorrectionReason);

public sealed record PostProcessMemoChangeRequest(
    [Range(1, long.MaxValue)] long ExpectedMemoVersion,
    string? Value);

public sealed record PostProcessCommitItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    PostProcessLifecycleChangeRequest? LifecycleChange,
    PostProcessMemoChangeRequest? MemoChange);

public sealed record PostProcessCommitRequest(
    [Required, MinLength(1)] IReadOnlyList<PostProcessCommitItemRequest> Items);
