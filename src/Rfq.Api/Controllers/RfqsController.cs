using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqsController(
    CreateDraft createDraft,
    UpdateInitialDraft updateInitialDraft,
    ConfirmInitialDraft confirmInitialDraft,
    ConfirmNewRfq confirmNewRfq,
    DiscardInitialDraft discardInitialDraft,
    GetActiveSalesRfqs getActiveSalesRfqs,
    PresentQuote presentQuote,
    UnpresentQuote unpresentQuote) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<InitialRfqResponse>> Create(
        CreateDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await createDraft.ExecuteAsync(ToCommand(request), cancellationToken);
            return Created("/api/rfqs/active-sales", ToResponse(result));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<InitialRfqResponse>> ConfirmNew(
        CreateDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await confirmNewRfq.ExecuteAsync(ToCommand(request), cancellationToken);
            return Created("/api/rfqs/active-sales", ToResponse(result));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    [HttpPut("{caseId:long}/draft")]
    public async Task<ActionResult<InitialRfqResponse>> UpdateDraft(
        long caseId,
        UpdateInitialDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await updateInitialDraft.ExecuteAsync(
                ToCommand(caseId, request),
                cancellationToken);
            return Ok(ToResponse(result));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("{caseId:long}/confirm")]
    public async Task<ActionResult<InitialRfqResponse>> ConfirmDraft(
        long caseId,
        UpdateInitialDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await confirmInitialDraft.ExecuteAsync(
                ToCommand(caseId, request),
                cancellationToken);
            return Ok(ToResponse(result));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("{caseId:long}/discard")]
    public async Task<IActionResult> DiscardDraft(
        long caseId,
        DiscardInitialDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await discardInitialDraft.ExecuteAsync(
                caseId,
                request.ExpectedVersion,
                cancellationToken);
            return NoContent();
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    [HttpGet("active-sales")]
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
            item.QuoteStatus,
            item.QuoteRequestReason,
            item.CurrentRevisionId,
            item.CurrentQuoteId,
            item.CurrentVersion,
            item.RevisionStatus,
            item.ContactOwnerId,
            item.AssignedTraderId,
            item.SettlementDate,
            item.StandardSettlementDate,
            item.Notional,
            item.SalesAndTradingMessage,
            item.Version,
            item.CreatedAt)));
    }

    [HttpPost("{caseId:long}/present")]
    public Task<ActionResult<PresentationResult>> Present(
        long caseId,
        PresentationRequest request,
        CancellationToken cancellationToken) => ExecutePresentationAsync(() =>
            presentQuote.ExecuteAsync(caseId, request.ExpectedCurrentVersion, cancellationToken));

    [HttpPost("{caseId:long}/unpresent")]
    public Task<ActionResult<PresentationResult>> Unpresent(
        long caseId,
        PresentationRequest request,
        CancellationToken cancellationToken) => ExecutePresentationAsync(() =>
            unpresentQuote.ExecuteAsync(caseId, request.ExpectedCurrentVersion, cancellationToken));

    private async Task<ActionResult<PresentationResult>> ExecutePresentationAsync(
        Func<Task<PresentationResult>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    private static CreateDraftCommand ToCommand(CreateDraftRequest request) => new(
        request.ClientId,
        request.SecurityId,
        request.Notional,
        request.SettlementDate,
        request.SalesAndTradingMessage,
        request.AssignedTraderId);

    private static UpdateInitialDraftCommand ToCommand(
        long caseId,
        UpdateInitialDraftRequest request) => new(
        caseId,
        request.Notional,
        request.SettlementDate,
        request.SalesAndTradingMessage,
        request.AssignedTraderId,
        request.ExpectedVersion);

    private static InitialRfqResponse ToResponse(InitialRfqResult result) => new(
        result.CaseId,
        result.RevisionId,
        result.RfqStatus,
        result.RevisionStatus,
        result.QuoteStatus,
        result.QuoteRequestReason,
        result.CategoryId,
        result.ContactOwnerId,
        result.AssignedTraderId,
        result.Notional,
        result.SettlementDate,
        result.StandardSettlementDate,
        result.SalesAndTradingMessage,
        result.Version,
        result.CreatedAt);

    private static bool IsExpected(Exception exception) =>
        exception is ArgumentException
            or KeyNotFoundException
            or InvalidOperationException
            or UnauthorizedAccessException;

    private ObjectResult ToProblem(Exception exception)
    {
        var statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Problem(statusCode: statusCode, detail: exception.Message);
    }
}

public sealed record CreateDraftRequest(
    [Required, MinLength(1)] string ClientId,
    [Required, MinLength(1)] string SecurityId,
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    string? AssignedTraderId);

public sealed record UpdateInitialDraftRequest(
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    string? AssignedTraderId,
    [Range(1, long.MaxValue)] long ExpectedVersion);

public sealed record DiscardInitialDraftRequest(
    [Range(1, long.MaxValue)] long ExpectedVersion);

public sealed record PresentationRequest(
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record InitialRfqResponse(
    long CaseId,
    Guid RevisionId,
    string RfqStatus,
    string RevisionStatus,
    string? QuoteStatus,
    string? QuoteRequestReason,
    string CategoryId,
    string ContactOwnerId,
    string AssignedTraderId,
    decimal? Notional,
    DateOnly? SettlementDate,
    DateOnly StandardSettlementDate,
    string SalesAndTradingMessage,
    long Version,
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
    string? QuoteStatus,
    string? QuoteRequestReason,
    Guid CurrentRevisionId,
    Guid? CurrentQuoteId,
    long CurrentVersion,
    string RevisionStatus,
    string ContactOwnerId,
    string AssignedTraderId,
    DateOnly? SettlementDate,
    DateOnly StandardSettlementDate,
    decimal? Notional,
    string SalesAndTradingMessage,
    long Version,
    DateTimeOffset CreatedAt);
