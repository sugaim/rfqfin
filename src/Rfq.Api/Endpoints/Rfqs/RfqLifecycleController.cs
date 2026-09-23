using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqLifecycle;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqLifecycleController(
    CloseHitRfq closeHit,
    CorrectOutcomeToHit correctToHit,
    CorrectOutcomeToAway correctToAway,
    PresentRfqs present,
    UnpresentRfqs unpresent,
    CloseAwayRfqs closeAway,
    CancelRfqs cancel,
    ReopenRfqs reopen) : ControllerBase
{
    [HttpPost("{caseId:long}/close/hit")]
    [EndpointName("CloseHitRfq")]
    public async Task<CloseResponse> CloseHit(
        long caseId, CloseHitRfqRequest request, CancellationToken token) =>
        LifecycleApiMapper.ToApi(await closeHit.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("{caseId:long}/outcome/correct-to-hit")]
    [EndpointName("CorrectOutcomeToHit")]
    public async Task<CloseResponse> CorrectToHit(
        long caseId, CorrectOutcomeToHitRequest request, CancellationToken token) =>
        LifecycleApiMapper.ToApi(await correctToHit.ExecuteAsync(
            new CaseId(caseId),
            request.Reason,
            new StateVersion(request.ExpectedCurrentVersion),
            token));

    [HttpPost("{caseId:long}/outcome/correct-to-away")]
    [EndpointName("CorrectOutcomeToAway")]
    public async Task<CloseResponse> CorrectToAway(
        long caseId, CorrectOutcomeToAwayRequest request, CancellationToken token) =>
        LifecycleApiMapper.ToApi(await correctToAway.ExecuteAsync(
            new CaseId(caseId),
            request.Reason,
            new StateVersion(request.ExpectedCurrentVersion),
            token));

    [HttpPost("present")]
    [EndpointName("PresentRfqs")]
    public Task<IReadOnlyList<CaseOperationResponse>> Present(
        PresentRfqsRequest request, CancellationToken token) =>
        ExecuteAsync(request.Items, present.ExecuteAsync, token);

    [HttpPost("unpresent")]
    [EndpointName("UnpresentRfqs")]
    public Task<IReadOnlyList<CaseOperationResponse>> Unpresent(
        UnpresentRfqsRequest request, CancellationToken token) =>
        ExecuteAsync(request.Items, unpresent.ExecuteAsync, token);

    [HttpPost("close-away")]
    [EndpointName("CloseAwayRfqs")]
    public Task<IReadOnlyList<CaseOperationResponse>> CloseAway(
        CloseAwayRfqsRequest request, CancellationToken token) =>
        ExecuteAsync(request.Items, closeAway.ExecuteAsync, token);

    [HttpPost("cancel")]
    [EndpointName("CancelRfqs")]
    public Task<IReadOnlyList<CaseOperationResponse>> Cancel(
        CancelRfqsRequest request, CancellationToken token) =>
        ExecuteAsync(request.Items, cancel.ExecuteAsync, token);

    [HttpPost("reopen")]
    [EndpointName("ReopenRfqs")]
    public Task<IReadOnlyList<CaseOperationResponse>> Reopen(
        ReopenRfqsRequest request, CancellationToken token) =>
        ExecuteAsync(request.Items, reopen.ExecuteAsync, token);

    private static async Task<IReadOnlyList<CaseOperationResponse>> ExecuteAsync(
        IReadOnlyList<CaseVersionItemRequest> items,
        Func<
            IReadOnlyList<LifecycleItem>,
            CancellationToken,
            Task<IReadOnlyList<CaseOperationResult>>> operation,
        CancellationToken token) =>
        [.. (await operation(
            [.. items.Select(item => new LifecycleItem(
                new CaseId(item.CaseId), new StateVersion(item.ExpectedCurrentVersion)))],
            token)).Select(CaseOperationApiMapper.ToApi)];
}

public sealed record CloseHitRfqRequest(
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record CorrectOutcomeToHitRequest(
    [Required] string Reason,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record CorrectOutcomeToAwayRequest(
    [Required] string Reason,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record CaseVersionItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record PresentRfqsRequest(
    [Required, MinLength(1)] IReadOnlyList<CaseVersionItemRequest> Items);

public sealed record UnpresentRfqsRequest(
    [Required, MinLength(1)] IReadOnlyList<CaseVersionItemRequest> Items);

public sealed record CloseAwayRfqsRequest(
    [Required, MinLength(1)] IReadOnlyList<CaseVersionItemRequest> Items);

public sealed record CancelRfqsRequest(
    [Required, MinLength(1)] IReadOnlyList<CaseVersionItemRequest> Items);

public sealed record ReopenRfqsRequest(
    [Required, MinLength(1)] IReadOnlyList<CaseVersionItemRequest> Items);

public sealed record CloseResponse(
    long CaseId,
    Api.RfqStatus RfqStatus,
    Guid ClosedQuoteId,
    bool Owned,
    long CurrentVersion);

public static class LifecycleApiMapper
{
    public static CloseResponse ToApi(CloseRfqResult value) => new(
        value.CaseId.Value,
        Enum.Parse<Api.RfqStatus>(value.RfqStatus.ToString()),
        value.ClosedQuoteId.Value,
        value.Owned,
        value.CurrentVersion.Value);
}
