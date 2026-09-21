using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqOwnership;

[ApiController]
[Route("api/rfqs/{caseId:long}")]
public sealed class RfqOwnershipController(
    PickUpRfq pickUp, ReleaseRfq release, AssignTrader assign, TakeOverRfq takeOver,
    BulkPickUpRfqs bulkPickUp, BulkReleaseRfqs bulkRelease,
    BulkAssignTrader bulkAssign) : ControllerBase
{
    [HttpPost("ownership/pick-up")]
    public async Task<OwnershipResponse> PickUp(long caseId, ConfirmedRequest request,
        CancellationToken token) => OwnershipApiMapper.ToApi(await pickUp.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedVersion), request.Confirmed, token));

    [HttpPost("ownership/release")]
    public async Task<OwnershipResponse> Release(long caseId, VersionRequest request,
        CancellationToken token) => OwnershipApiMapper.ToApi(await release.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedVersion), token));

    [HttpPost("ownership/take-over")]
    public async Task<OwnershipResponse> TakeOver(long caseId, ConfirmedRequest request,
        CancellationToken token) => OwnershipApiMapper.ToApi(await takeOver.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedVersion), request.Confirmed, token));

    [HttpPut("assigned-trader")]
    public async Task<OwnershipResponse> Assign(long caseId, AssignTraderRequest request,
        CancellationToken token) => OwnershipApiMapper.ToApi((await assign.ExecuteAsync(
            new CaseId(caseId), UserId.Create(request.AssignedTraderId),
            new StateVersion(request.ExpectedVersion), token)).Rfq);

    [HttpPost("/api/rfqs/ownership/bulk-pick-up")]
    public async Task<IReadOnlyList<BulkItemResponse>> BulkPickUp(
        BulkPickUpRequest request, CancellationToken token) =>
        (await bulkPickUp.ExecuteAsync(request.Items.Select(item => new PickUpRfqItem(
            new CaseId(item.CaseId), new StateVersion(item.ExpectedVersion), item.Confirmed)).ToArray(),
            token)).Select(BulkApiMapper.ToApi).ToArray();

    [HttpPost("/api/rfqs/ownership/bulk-release")]
    public async Task<IReadOnlyList<BulkItemResponse>> BulkRelease(
        BulkOwnershipRequest request, CancellationToken token) =>
        (await bulkRelease.ExecuteAsync(OwnershipApiMapper.ToItems(request.Items), token))
        .Select(BulkApiMapper.ToApi).ToArray();

    [HttpPost("/api/rfqs/ownership/bulk-assign-trader")]
    public async Task<IReadOnlyList<BulkItemResponse>> BulkAssign(
        BulkAssignTraderRequest request, CancellationToken token) =>
        (await bulkAssign.ExecuteAsync(UserId.Create(request.TargetAssignedTraderId),
            OwnershipApiMapper.ToItems(request.Items), token))
        .Select(BulkApiMapper.ToApi).ToArray();
}

public sealed record VersionRequest([Range(1, long.MaxValue)] long ExpectedVersion);
public sealed record ConfirmedRequest([Range(1, long.MaxValue)] long ExpectedVersion, bool Confirmed);
public sealed record AssignTraderRequest([Required, MinLength(1)] string AssignedTraderId,
    [Range(1, long.MaxValue)] long ExpectedVersion);
public sealed record OwnershipItemRequest([Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedVersion);
public sealed record PickUpItemRequest([Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedVersion, bool Confirmed);
public sealed record BulkPickUpRequest(
    [Required, MinLength(1)] IReadOnlyList<PickUpItemRequest> Items);
public sealed record BulkOwnershipRequest(
    [Required, MinLength(1)] IReadOnlyList<OwnershipItemRequest> Items);
public sealed record BulkAssignTraderRequest(
    [Required, MinLength(1)] string TargetAssignedTraderId,
    [Required, MinLength(1)] IReadOnlyList<OwnershipItemRequest> Items);
public sealed record OwnershipResponse(long CaseId, string AssignedTraderId,
    bool Owned, long CurrentVersion);
public static class OwnershipApiMapper
{
    public static OwnershipResponse ToApi(OwnershipResult value) => new(value.CaseId.Value,
        value.AssignedTraderId.Value, value.Owned, value.CurrentVersion.Value);
    public static OwnershipBulkItem[] ToItems(IReadOnlyList<OwnershipItemRequest> items) =>
        items.Select(item => new OwnershipBulkItem(new CaseId(item.CaseId),
            new StateVersion(item.ExpectedVersion))).ToArray();
}
