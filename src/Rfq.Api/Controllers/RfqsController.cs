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
                new CaseId(caseId),
                new StateVersion(request.ExpectedVersion),
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
            item.CaseId.Value,
            item.ClientId.Value,
            item.ClientName,
            item.SecurityId.Value,
            item.SecurityJapaneseName,
            item.SecurityBbgDisplay,
            item.CategoryId.Value,
            item.RfqStatus.ToString(),
            item.QuoteStatus?.ToString(),
            item.QuoteRequestReason?.ToString(),
            item.CurrentRevisionId.Value,
            item.CurrentQuoteId?.Value,
            item.ClosedQuoteId?.Value,
            item.CurrentVersion.Value,
            item.RevisionStatus.ToString(),
            item.ContactOwnerId.Value,
            item.AssignedTraderId.Value,
            item.SettlementDate,
            item.StandardSettlementDate,
            item.Notional,
            item.SalesAndTradingMessage,
            item.SalesMemo,
            item.MemoVersion.Value,
            item.Version.Value,
            item.CreatedAt,
            item.DraftRevisionId?.Value,
            item.DraftVersion?.Value,
            item.DraftSettlementDate,
            item.DraftNotional,
            item.DraftSalesAndTradingMessage)));
    }

    [HttpPut("{caseId:long}/amendment")]
    public Task<ActionResult<AmendmentResponse>> SaveAmendment(
        long caseId,
        SaveAmendmentRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            saveAmendment.ExecuteAsync(new SaveAmendmentCommand(
                new CaseId(caseId),
                request.Notional,
                request.SettlementDate,
                request.SalesAndTradingMessage,
                new StateVersion(request.ExpectedCurrentVersion),
                request.ExpectedDraftVersion is null ? null : new StateVersion(request.ExpectedDraftVersion.Value)), cancellationToken),
            AmendmentResponse.From);

    [HttpPost("{caseId:long}/amendment/confirm")]
    public Task<ActionResult<AmendmentResponse>> ConfirmAmendment(
        long caseId,
        AmendmentActionRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            confirmAmendment.ExecuteAsync(new AmendmentItem(
                new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), new StateVersion(request.ExpectedDraftVersion)),
                cancellationToken), AmendmentResponse.From);

    [HttpPost("{caseId:long}/amendment/discard")]
    public Task<ActionResult<AmendmentResponse>> DiscardAmendment(
        long caseId,
        AmendmentActionRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            discardAmendment.ExecuteAsync(new AmendmentItem(
                new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), new StateVersion(request.ExpectedDraftVersion)),
                cancellationToken), AmendmentResponse.From);

    [HttpPost("amendment/bulk-confirm")]
    public Task<ActionResult<IReadOnlyList<AmendmentItemResponse>>> BulkConfirmAmendment(
        BulkAmendmentRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            bulkConfirmAmendments.ExecuteAsync(request.Items.Select(ToItem).ToArray(), cancellationToken),
            items => (IReadOnlyList<AmendmentItemResponse>)items.Select(AmendmentItemResponse.From).ToArray());

    [HttpPost("amendment/bulk-discard")]
    public Task<ActionResult<IReadOnlyList<AmendmentItemResponse>>> BulkDiscardAmendment(
        BulkAmendmentRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            bulkDiscardAmendments.ExecuteAsync(request.Items.Select(ToItem).ToArray(), cancellationToken),
            items => (IReadOnlyList<AmendmentItemResponse>)items.Select(AmendmentItemResponse.From).ToArray());

    [HttpPost("{caseId:long}/create-from-existing")]
    public async Task<ActionResult<InitialRfqResponse>> CreateFromExisting(
        long caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await createFromExisting.ExecuteAsync(new CaseId(caseId), cancellationToken);
            return Created("/api/rfqs/active-sales", ToResponse(result));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("{caseId:long}/cancel")]
    public Task<ActionResult<LifecycleResponse>> Cancel(
        long caseId, LifecycleActionRequest request, CancellationToken cancellationToken) =>
        ExecuteCaseActionAsync(() => cancelRfq.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), cancellationToken),
            LifecycleResponse.From);

    [HttpPost("{caseId:long}/reopen")]
    public Task<ActionResult<LifecycleResponse>> Reopen(
        long caseId, LifecycleActionRequest request, CancellationToken cancellationToken) =>
        ExecuteCaseActionAsync(() => reopenRfq.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), cancellationToken),
            LifecycleResponse.From);

    private static AmendmentItem ToItem(AmendmentActionItemRequest item) =>
        new(new CaseId(item.CaseId), new StateVersion(item.ExpectedCurrentVersion), new StateVersion(item.ExpectedDraftVersion));

    [HttpPost("{caseId:long}/present")]
    public Task<ActionResult<PresentationResponse>> Present(
        long caseId,
        PresentationRequest request,
        CancellationToken cancellationToken) => ExecutePresentationAsync(() =>
            presentQuote.ExecuteAsync(new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), cancellationToken));

    [HttpPost("{caseId:long}/unpresent")]
    public Task<ActionResult<PresentationResponse>> Unpresent(
        long caseId,
        PresentationRequest request,
        CancellationToken cancellationToken) => ExecutePresentationAsync(() =>
            unpresentQuote.ExecuteAsync(new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), cancellationToken));

    [HttpPost("{caseId:long}/close")]
    public Task<ActionResult<CloseRfqResponse>> Close(
        long caseId,
        CloseRfqRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            closeRfq.ExecuteAsync(
                new CaseId(caseId),
                ParseOutcome(request.Outcome),
                new StateVersion(request.ExpectedCurrentVersion),
                cancellationToken), CloseRfqResponse.From);

    [HttpPost("bulk-close")]
    public Task<ActionResult<IReadOnlyList<BulkCloseItemResponse>>> BulkClose(
        BulkCloseRfqRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            bulkCloseRfqs.ExecuteAsync(
                request.Items.Select(item => new BulkCloseItem(
                    new CaseId(item.CaseId),
                    new StateVersion(item.ExpectedCurrentVersion))).ToArray(),
                ParseOutcome(request.Outcome),
                cancellationToken),
            items => (IReadOnlyList<BulkCloseItemResponse>)items.Select(BulkCloseItemResponse.From).ToArray());

    [HttpPost("{caseId:long}/correct-outcome")]
    public Task<ActionResult<CloseRfqResponse>> CorrectOutcome(
        long caseId,
        CorrectOutcomeRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            correctRfqOutcome.ExecuteAsync(
                new CaseId(caseId),
                ParseOutcome(request.Outcome),
                request.Reason,
                new StateVersion(request.ExpectedCurrentVersion),
                cancellationToken), CloseRfqResponse.From);

    [HttpPost("{caseId:long}/contact-owner")]
    public Task<ActionResult<ContactOwnerResponse>> ChangeOwner(
        long caseId,
        ChangeContactOwnerRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            changeContactOwner.ExecuteAsync(
                new CaseId(caseId),
                UserId.Create(request.TargetUserId),
                new StateVersion(request.ExpectedCurrentVersion),
                request.Confirmed,
                cancellationToken), ContactOwnerResponse.From);

    [HttpPut("{caseId:long}/sales-memo")]
    public Task<ActionResult<CaseMemoResponse>> UpdateSalesMemo(
        long caseId,
        UpdateMemoRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            updateSalesMemo.ExecuteAsync(
                new CaseId(caseId),
                request.Memo,
                new StateVersion(request.ExpectedVersion),
                cancellationToken), CaseMemoResponse.From);

    [HttpPut("{caseId:long}/trader-memo")]
    public Task<ActionResult<CaseMemoResponse>> UpdateTraderMemo(
        long caseId,
        UpdateMemoRequest request,
        CancellationToken cancellationToken) => ExecuteCaseActionAsync(() =>
            updateTraderMemo.ExecuteAsync(
                new CaseId(caseId),
                request.Memo,
                new StateVersion(request.ExpectedVersion),
                cancellationToken), CaseMemoResponse.From);

    private async Task<ActionResult<PresentationResponse>> ExecutePresentationAsync(
        Func<Task<PresentationResult>> action)
    {
        try
        {
            return Ok(PresentationResponse.From(await action()));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    private async Task<ActionResult<TResponse>> ExecuteCaseActionAsync<TApplication, TResponse>(
        Func<Task<TApplication>> action,
        Func<TApplication, TResponse> map)
    {
        try
        {
            return Ok(map(await action()));
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
        ClientId.Create(request.ClientId),
        SecurityId.Create(request.SecurityId),
        request.Notional,
        request.SettlementDate,
        request.SalesAndTradingMessage,
        request.AssignedTraderId is null ? null : UserId.Create(request.AssignedTraderId));

    private static UpdateInitialDraftCommand ToCommand(
        long caseId,
        UpdateInitialDraftRequest request) => new(
        new CaseId(caseId),
        request.Notional,
        request.SettlementDate,
        request.SalesAndTradingMessage,
        request.AssignedTraderId is null ? null : UserId.Create(request.AssignedTraderId),
        new StateVersion(request.ExpectedVersion));

    private static InitialRfqResponse ToResponse(InitialRfqResult result) => new(
        result.CaseId.Value,
        result.RevisionId.Value,
        result.RfqStatus.ToString(),
        result.RevisionStatus.ToString(),
        result.QuoteStatus?.ToString(),
        result.QuoteRequestReason?.ToString(),
        result.CategoryId.Value,
        result.ContactOwnerId.Value,
        result.AssignedTraderId.Value,
        result.Notional,
        result.SettlementDate,
        result.StandardSettlementDate,
        result.SalesAndTradingMessage,
        result.Version.Value,
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

public sealed record AmendmentResponse(
    long CaseId,
    Guid CurrentRevisionId,
    Guid? DraftRevisionId,
    long CurrentVersion,
    long? DraftVersion,
    string RfqStatus,
    string? QuoteStatus,
    string? QuoteRequestReason)
{
    public static AmendmentResponse From(AmendmentResult result) => new(
        result.CaseId.Value, result.CurrentRevisionId.Value, result.DraftRevisionId?.Value,
        result.CurrentVersion.Value, result.DraftVersion?.Value, result.RfqStatus.ToString(),
        result.QuoteStatus?.ToString(), result.QuoteRequestReason?.ToString());
}

public sealed record AmendmentItemResponse(long CaseId, string Result, string? Error)
{
    public static AmendmentItemResponse From(AmendmentItemResult result) =>
        new(result.CaseId.Value, result.Result, result.Error);
}

public sealed record LifecycleResponse(
    long CaseId,
    string RfqStatus,
    string? QuoteStatus,
    string? QuoteRequestReason,
    long CurrentVersion)
{
    public static LifecycleResponse From(LifecycleResult result) => new(
        result.CaseId.Value, result.RfqStatus.ToString(), result.QuoteStatus?.ToString(),
        result.QuoteRequestReason?.ToString(), result.CurrentVersion.Value);
}

public sealed record PresentationResponse(
    long CaseId,
    Guid QuoteId,
    string RfqStatus,
    string QuoteStatus,
    long CurrentVersion)
{
    public static PresentationResponse From(PresentationResult result) => new(
        result.CaseId.Value, result.QuoteId.Value, result.RfqStatus.ToString(),
        result.QuoteStatus.ToString(), result.CurrentVersion.Value);
}

public sealed record CloseRfqResponse(
    long CaseId,
    string RfqStatus,
    Guid ClosedQuoteId,
    bool Owned,
    long CurrentVersion)
{
    public static CloseRfqResponse From(CloseRfqResult result) => new(
        result.CaseId.Value, result.RfqStatus.ToString(), result.ClosedQuoteId.Value,
        result.Owned, result.CurrentVersion.Value);
}

public sealed record BulkCloseItemResponse(
    long CaseId,
    string Result,
    string? RfqStatus,
    string? Error)
{
    public static BulkCloseItemResponse From(BulkCloseItemResult result) => new(
        result.CaseId.Value, result.Result, result.RfqStatus?.ToString(), result.Error);
}

public sealed record ContactOwnerResponse(long CaseId, string ContactOwnerId, long CurrentVersion)
{
    public static ContactOwnerResponse From(ContactOwnerResult result) => new(
        result.CaseId.Value, result.ContactOwnerId.Value, result.CurrentVersion.Value);
}

public sealed record CaseMemoResponse(long CaseId, string Memo, long Version)
{
    public static CaseMemoResponse From(CaseMemoResult result) =>
        new(result.CaseId.Value, result.Memo, result.Version.Value);
}
