using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

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
    UnpresentQuote unpresentQuote,
    CloseRfq closeRfq,
    BulkCloseRfqs bulkCloseRfqs,
    CorrectRfqOutcome correctRfqOutcome,
    ChangeContactOwner changeContactOwner,
    UpdateSalesMemo updateSalesMemo,
    UpdateTraderMemo updateTraderMemo,
    SaveAmendment saveAmendment,
    ConfirmAmendment confirmAmendment,
    DiscardAmendment discardAmendment,
    BulkConfirmAmendments bulkConfirmAmendments,
    BulkDiscardAmendments bulkDiscardAmendments,
    CreateFromExisting createFromExisting,
    CancelRfq cancelRfq,
    ReopenRfq reopenRfq) : ControllerBase
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
            item.ClosedQuoteId,
            item.CurrentVersion,
            item.RevisionStatus,
            item.ContactOwnerId,
            item.AssignedTraderId,
            item.SettlementDate,
            item.StandardSettlementDate,
            item.Notional,
            item.SalesAndTradingMessage,
            item.SalesMemo,
            item.MemoVersion,
            item.Version,
            item.CreatedAt,
            item.DraftRevisionId,
            item.DraftVersion,
            item.DraftSettlementDate,
            item.DraftNotional,
            item.DraftSalesAndTradingMessage)));
    }

    [HttpPut("{caseId:long}/amendment")]
    public Task<ActionResult<AmendmentResult>> SaveAmendment(
        long caseId,
        SaveAmendmentRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            saveAmendment.ExecuteAsync(new SaveAmendmentCommand(
                caseId,
                request.Notional,
                request.SettlementDate,
                request.SalesAndTradingMessage,
                request.ExpectedCurrentVersion,
                request.ExpectedDraftVersion), cancellationToken));

    [HttpPost("{caseId:long}/amendment/confirm")]
    public Task<ActionResult<AmendmentResult>> ConfirmAmendment(
        long caseId,
        AmendmentActionRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            confirmAmendment.ExecuteAsync(new AmendmentItem(
                caseId, request.ExpectedCurrentVersion, request.ExpectedDraftVersion),
                cancellationToken));

    [HttpPost("{caseId:long}/amendment/discard")]
    public Task<ActionResult<AmendmentResult>> DiscardAmendment(
        long caseId,
        AmendmentActionRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            discardAmendment.ExecuteAsync(new AmendmentItem(
                caseId, request.ExpectedCurrentVersion, request.ExpectedDraftVersion),
                cancellationToken));

    [HttpPost("amendment/bulk-confirm")]
    public Task<ActionResult<IReadOnlyList<AmendmentItemResult>>> BulkConfirmAmendment(
        BulkAmendmentRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            bulkConfirmAmendments.ExecuteAsync(request.Items.Select(ToItem).ToArray(), cancellationToken));

    [HttpPost("amendment/bulk-discard")]
    public Task<ActionResult<IReadOnlyList<AmendmentItemResult>>> BulkDiscardAmendment(
        BulkAmendmentRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            bulkDiscardAmendments.ExecuteAsync(request.Items.Select(ToItem).ToArray(), cancellationToken));

    [HttpPost("{caseId:long}/create-from-existing")]
    public async Task<ActionResult<InitialRfqResponse>> CreateFromExisting(
        long caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await createFromExisting.ExecuteAsync(caseId, cancellationToken);
            return Created("/api/rfqs/active-sales", ToResponse(result));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("{caseId:long}/cancel")]
    public Task<ActionResult<LifecycleResult>> Cancel(
        long caseId, LifecycleActionRequest request, CancellationToken cancellationToken) =>
        ExecuteCaseActionAsync(() => cancelRfq.ExecuteAsync(
            caseId, request.ExpectedCurrentVersion, cancellationToken));

    [HttpPost("{caseId:long}/reopen")]
    public Task<ActionResult<LifecycleResult>> Reopen(
        long caseId, LifecycleActionRequest request, CancellationToken cancellationToken) =>
        ExecuteCaseActionAsync(() => reopenRfq.ExecuteAsync(
            caseId, request.ExpectedCurrentVersion, cancellationToken));

    private static AmendmentItem ToItem(AmendmentActionItemRequest item) =>
        new(item.CaseId, item.ExpectedCurrentVersion, item.ExpectedDraftVersion);

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

    [HttpPost("{caseId:long}/close")]
    public Task<ActionResult<CloseRfqResult>> Close(
        long caseId,
        CloseRfqRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            closeRfq.ExecuteAsync(
                caseId,
                ParseOutcome(request.Outcome),
                request.ExpectedCurrentVersion,
                cancellationToken));

    [HttpPost("bulk-close")]
    public Task<ActionResult<IReadOnlyList<BulkCloseItemResult>>> BulkClose(
        BulkCloseRfqRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            bulkCloseRfqs.ExecuteAsync(
                request.Items.Select(item => new BulkCloseItem(
                    item.CaseId,
                    item.ExpectedCurrentVersion)).ToArray(),
                ParseOutcome(request.Outcome),
                cancellationToken));

    [HttpPost("{caseId:long}/correct-outcome")]
    public Task<ActionResult<CloseRfqResult>> CorrectOutcome(
        long caseId,
        CorrectOutcomeRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            correctRfqOutcome.ExecuteAsync(
                caseId,
                ParseOutcome(request.Outcome),
                request.Reason,
                request.ExpectedCurrentVersion,
                cancellationToken));

    [HttpPost("{caseId:long}/contact-owner")]
    public Task<ActionResult<ContactOwnerResult>> ChangeOwner(
        long caseId,
        ChangeContactOwnerRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            changeContactOwner.ExecuteAsync(
                caseId,
                request.TargetUserId,
                request.ExpectedCurrentVersion,
                request.Confirmed,
                cancellationToken));

    [HttpPut("{caseId:long}/sales-memo")]
    public Task<ActionResult<CaseMemoResult>> UpdateSalesMemo(
        long caseId,
        UpdateMemoRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            updateSalesMemo.ExecuteAsync(
                caseId,
                request.Memo,
                request.ExpectedVersion,
                cancellationToken));

    [HttpPut("{caseId:long}/trader-memo")]
    public Task<ActionResult<CaseMemoResult>> UpdateTraderMemo(
        long caseId,
        UpdateMemoRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            updateTraderMemo.ExecuteAsync(
                caseId,
                request.Memo,
                request.ExpectedVersion,
                cancellationToken));

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

    private async Task<ActionResult<T>> ExecuteCaseActionAsync<T>(Func<Task<T>> action)
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

    private static RfqStatus ParseOutcome(string outcome)
    {
        if (!Enum.TryParse<RfqStatus>(outcome, false, out var parsed)
            || parsed is not RfqStatus.Hit and not RfqStatus.Away)
        {
            throw new ArgumentException("Outcome must be Hit or Away.", nameof(outcome));
        }

        return parsed;
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
            or UnauthorizedAccessException
            or StateVersionMismatchException
            or DomainRuleViolationException
            or DomainValidationException;

    private ObjectResult ToProblem(Exception exception)
    {
        var statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            StateVersionMismatchException => StatusCodes.Status409Conflict,
            DomainRuleViolationException => StatusCodes.Status409Conflict,
            DomainValidationException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ErrorCode(exception),
            Detail = exception.Message,
        };
        problem.Extensions["code"] = ErrorCode(exception);
        return StatusCode(statusCode, problem);
    }

    private static string ErrorCode(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Forbidden",
        KeyNotFoundException => "NotFound",
        StateVersionMismatchException or DomainRuleViolationException => "Conflict",
        DomainValidationException => "Validation",
        InvalidOperationException => "Conflict",
        _ => "Validation",
    };
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

public sealed record CloseRfqRequest(
    [Required, MinLength(1)] string Outcome,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record BulkCloseRfqItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record BulkCloseRfqRequest(
    [Required, MinLength(1)] string Outcome,
    [Required, MinLength(1)] IReadOnlyList<BulkCloseRfqItemRequest> Items);

public sealed record CorrectOutcomeRequest(
    [Required, MinLength(1)] string Outcome,
    string? Reason,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record ChangeContactOwnerRequest(
    [Required, MinLength(1)] string TargetUserId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    bool Confirmed);

public sealed record UpdateMemoRequest(
    string? Memo,
    [Range(1, long.MaxValue)] long ExpectedVersion);

public sealed record SaveAmendmentRequest(
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    long? ExpectedDraftVersion);

public sealed record AmendmentActionRequest(
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedDraftVersion);

public sealed record AmendmentActionItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedDraftVersion);

public sealed record BulkAmendmentRequest(
    [Required, MinLength(1)] IReadOnlyList<AmendmentActionItemRequest> Items);

public sealed record LifecycleActionRequest(
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
    Guid? ClosedQuoteId,
    long CurrentVersion,
    string RevisionStatus,
    string ContactOwnerId,
    string AssignedTraderId,
    DateOnly? SettlementDate,
    DateOnly StandardSettlementDate,
    decimal? Notional,
    string SalesAndTradingMessage,
    string SalesMemo,
    long MemoVersion,
    long Version,
    DateTimeOffset CreatedAt,
    Guid? DraftRevisionId,
    long? DraftVersion,
    DateOnly? DraftSettlementDate,
    decimal? DraftNotional,
    string? DraftSalesAndTradingMessage);
