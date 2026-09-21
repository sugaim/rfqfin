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
                new CreateDraftCommand(
                    request.ClientId,
                    request.SecurityId,
                    request.SettlementDate,
                    request.AssignedTraderId),
                cancellationToken);

            var response = new CreateDraftResponse(
                result.CaseId,
                result.RevisionId,
                result.RfqStatus,
                result.CategoryId,
                result.ContactOwnerId,
                result.AssignedTraderId,
                result.SettlementDate,
                result.StandardSettlementDate,
                result.CreatedAt);

            return Created("/api/rfqs/active-sales", response);
        }
        catch (Exception exception) when (
            exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
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
            item.ClientName,
            item.SecurityId,
            item.SecurityJapaneseName,
            item.SecurityBbgDisplay,
            item.CategoryId,
            item.RfqStatus,
            item.CurrentRevisionId,
            item.RevisionStatus,
            item.ContactOwnerId,
            item.AssignedTraderId,
            item.SettlementDate,
            item.StandardSettlementDate,
            item.CreatedAt)));
    }
}

public sealed record CreateDraftRequest(
    [Required, MinLength(1)] string ClientId,
    [Required, MinLength(1)] string SecurityId,
    DateOnly SettlementDate,
    string? AssignedTraderId);

public sealed record CreateDraftResponse(
    long CaseId,
    Guid RevisionId,
    string RfqStatus,
    string CategoryId,
    string ContactOwnerId,
    string AssignedTraderId,
    DateOnly SettlementDate,
    DateOnly StandardSettlementDate,
    DateTimeOffset CreatedAt);

public sealed record SalesRfqResponse(
    long CaseId,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityJapaneseName,
    string SecurityBbgDisplay,
    string CategoryId,
    string RfqStatus,
    Guid CurrentRevisionId,
    string RevisionStatus,
    string ContactOwnerId,
    string AssignedTraderId,
    DateOnly SettlementDate,
    DateOnly StandardSettlementDate,
    DateTimeOffset CreatedAt);
