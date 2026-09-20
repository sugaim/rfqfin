using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqsController(
    CreateDraft createDraft,
    GetActiveSalesRfqs getActiveSalesRfqs) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateDraftResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateDraftResponse>> Create(
        CreateDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await createDraft.ExecuteAsync(
                new CreateDraftCommand(request.ClientId, request.SecurityId),
                cancellationToken);

            var response = new CreateDraftResponse(
                result.CaseId,
                result.RevisionId,
                result.RfqStatus,
                result.CreatedAt);

            return Created("/api/rfqs/active-sales", response);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError("request", exception.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpGet("active-sales")]
    [ProducesResponseType<IReadOnlyList<SalesRfqResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SalesRfqResponse>>> GetActiveSales(
        CancellationToken cancellationToken)
    {
        var items = await getActiveSalesRfqs.ExecuteAsync(cancellationToken);
        return Ok(items.Select(item => new SalesRfqResponse(
            item.CaseId,
            item.ClientId,
            item.SecurityId,
            item.RfqStatus,
            item.CurrentRevisionId,
            item.RevisionStatus,
            item.CreatedAt)));
    }
}

public sealed record CreateDraftRequest(
    [Required, MinLength(1)] string ClientId,
    [Required, MinLength(1)] string SecurityId);

public sealed record CreateDraftResponse(
    Guid CaseId,
    Guid RevisionId,
    string RfqStatus,
    DateTimeOffset CreatedAt);

public sealed record SalesRfqResponse(
    Guid CaseId,
    string ClientId,
    string SecurityId,
    string RfqStatus,
    Guid CurrentRevisionId,
    string RevisionStatus,
    DateTimeOffset CreatedAt);
