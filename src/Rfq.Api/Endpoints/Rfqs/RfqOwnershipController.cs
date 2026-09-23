using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqOwnership;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqOwnershipController(
    PickUpRfqs pickUp,
    ReleaseRfqs release,
    AssignTraders assign,
    TakeOverRfqs takeOver) : ControllerBase
{
    [HttpPost("pick-up")]
    [EndpointName("PickUpRfqs")]
    public Task<IReadOnlyList<CaseOperationResponse>> PickUp(
        PickUpRfqsRequest request, CancellationToken token) =>
        ExecuteAsync(
            request.Items,
            (items, cancellationToken) => pickUp.ExecuteAsync(
                request.Confirmed, items, cancellationToken),
            token);

    [HttpPost("release")]
    [EndpointName("ReleaseRfqs")]
    public Task<IReadOnlyList<CaseOperationResponse>> Release(
        ReleaseRfqsRequest request, CancellationToken token) =>
        ExecuteAsync(request.Items, release.ExecuteAsync, token);

    [HttpPost("assign-trader")]
    [EndpointName("AssignTraders")]
    public Task<IReadOnlyList<CaseOperationResponse>> Assign(
        AssignTradersRequest request, CancellationToken token) =>
        ExecuteAsync(
            request.Items,
            (items, cancellationToken) => assign.ExecuteAsync(
                UserId.Create(request.TargetTraderId), items, cancellationToken),
            token);

    [HttpPost("take-over")]
    [EndpointName("TakeOverRfqs")]
    public Task<IReadOnlyList<CaseOperationResponse>> TakeOver(
        TakeOverRfqsRequest request, CancellationToken token) =>
        ExecuteAsync(
            request.Items,
            (items, cancellationToken) => takeOver.ExecuteAsync(
                request.Confirmed, items, cancellationToken),
            token);

    private static async Task<IReadOnlyList<CaseOperationResponse>> ExecuteAsync(
        IReadOnlyList<OwnershipItemRequest> items,
        Func<
            IReadOnlyList<OwnershipItem>,
            CancellationToken,
            Task<IReadOnlyList<CaseOperationResult>>> operation,
        CancellationToken token) =>
        [.. (await operation(
            [.. items.Select(item => new OwnershipItem(
                new CaseId(item.CaseId),
                new StateVersion(item.ExpectedCurrentVersion)))],
            token)).Select(CaseOperationApiMapper.ToApi)];
}

public sealed record OwnershipItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record PickUpRfqsRequest(
    bool Confirmed,
    [Required, MinLength(1)] IReadOnlyList<OwnershipItemRequest> Items);

public sealed record ReleaseRfqsRequest(
    [Required, MinLength(1)] IReadOnlyList<OwnershipItemRequest> Items);

public sealed record AssignTradersRequest(
    [Required, MinLength(1)] string TargetTraderId,
    [Required, MinLength(1)] IReadOnlyList<OwnershipItemRequest> Items);

public sealed record TakeOverRfqsRequest(
    bool Confirmed,
    [Required, MinLength(1)] IReadOnlyList<OwnershipItemRequest> Items);
