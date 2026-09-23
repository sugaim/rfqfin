using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqResponsibility;

[ApiController]
[Route("api/rfqs/change-contact-owner")]
public sealed class RfqResponsibilityController(ChangeContactOwners changeOwners) : ControllerBase
{
    [HttpPost]
    [EndpointName("ChangeContactOwners")]
    public async Task<IReadOnlyList<CaseOperationResponse>> ChangeContactOwners(
        ChangeContactOwnersRequest request,
        CancellationToken token) =>
        [.. (await changeOwners.ExecuteAsync(
            UserId.Create(request.TargetContactOwnerId),
            request.Confirmed,
            [.. request.Items.Select(item => new OwnershipItem(
                new CaseId(item.CaseId),
                new StateVersion(item.ExpectedCurrentVersion)))],
            token)).Select(CaseOperationApiMapper.ToApi)];
}

public sealed record ChangeContactOwnerItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record ChangeContactOwnersRequest(
    [Required, MinLength(1)] string TargetContactOwnerId,
    bool Confirmed,
    [Required, MinLength(1)] IReadOnlyList<ChangeContactOwnerItemRequest> Items);
