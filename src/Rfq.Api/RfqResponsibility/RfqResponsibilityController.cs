using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqResponsibility;

[ApiController]
[Route("api/rfqs/{caseId:long}/contact-owner")]
public sealed class RfqResponsibilityController(ChangeContactOwner changeOwner) : ControllerBase
{
    [HttpPut]
    public async Task<ContactOwnerResponse> Put(
        long caseId,
        ChangeContactOwnerRequest request,
        CancellationToken token)
    {
        ContactOwnerResult value = await changeOwner.ExecuteAsync(
            new CaseId(caseId),
            UserId.Create(request.ContactOwnerId),
            new StateVersion(request.ExpectedCurrentVersion),
            request.Confirmed,
            token);
        return new(value.CaseId.Value, value.ContactOwnerId.Value, value.CurrentVersion.Value);
    }
}

public sealed record ChangeContactOwnerRequest(
    [Required, MinLength(1)] string ContactOwnerId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    bool Confirmed);

public sealed record ContactOwnerResponse(long CaseId, string ContactOwnerId, long CurrentVersion);
