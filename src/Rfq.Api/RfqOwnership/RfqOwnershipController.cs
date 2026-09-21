using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqOwnership;

[ApiController]
[Route("api/rfqs/{caseId:long}")]
public sealed class RfqOwnershipController(
    PickUpRfq pickUp, ReleaseRfq release, AssignTrader assign, TakeOverRfq takeOver) : ControllerBase
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
        CancellationToken token) => OwnershipApiMapper.ToApi(await assign.ExecuteAsync(
            new CaseId(caseId), UserId.Create(request.AssignedTraderId),
            new StateVersion(request.ExpectedVersion), token));
}

public sealed record VersionRequest([Range(1, long.MaxValue)] long ExpectedVersion);
public sealed record ConfirmedRequest([Range(1, long.MaxValue)] long ExpectedVersion, bool Confirmed);
public sealed record AssignTraderRequest([Required, MinLength(1)] string AssignedTraderId,
    [Range(1, long.MaxValue)] long ExpectedVersion);
public sealed record OwnershipResponse(long CaseId, string AssignedTraderId,
    bool Owned, long CurrentVersion);
public static class OwnershipApiMapper
{
    public static OwnershipResponse ToApi(OwnershipResult value) => new(value.CaseId.Value,
        value.AssignedTraderId.Value, value.Owned, value.CurrentVersion.Value);
}
