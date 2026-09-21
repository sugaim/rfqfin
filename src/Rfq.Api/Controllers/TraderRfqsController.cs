using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/trader/rfqs")]
public sealed class TraderRfqsController(
    GetActiveTraderRfqs getActiveTraderRfqs,
    PickUpRfq pickUpRfq,
    ReleaseRfq releaseRfq,
    AssignTrader assignTrader,
    TakeOverRfq takeOverRfq,
    CalculateWorkingQuote calculateWorkingQuote,
    ChangeWorkingQuoteMode changeWorkingQuoteMode,
    UpdateManualWorkingQuote updateManualWorkingQuote,
    ConfirmQuote confirmQuote,
    WithdrawQuote withdrawQuote,
    BulkWithdrawQuotes bulkWithdrawQuotes) : ControllerBase
{
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<TraderRfqResponse>>> GetActive(
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await getActiveTraderRfqs.ExecuteAsync(cancellationToken);
            return Ok(items.Select(TraderRfqResponse.From).ToArray());
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("{caseId:long}/pick-up")]
    public Task<ActionResult<OwnershipResponse>> PickUp(
        long caseId,
        ConfirmedOwnershipRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(() =>
            pickUpRfq.ExecuteAsync(
                new CaseId(caseId),
                new StateVersion(request.ExpectedVersion),
                request.Confirmed,
                cancellationToken));

    [HttpPost("{caseId:long}/release")]
    public Task<ActionResult<OwnershipResponse>> Release(
        long caseId,
        OwnershipRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(() =>
            releaseRfq.ExecuteAsync(new CaseId(caseId), new StateVersion(request.ExpectedVersion), cancellationToken));

    [HttpPost("{caseId:long}/assign")]
    public Task<ActionResult<OwnershipResponse>> Assign(
        long caseId,
        AssignTraderRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(() =>
            assignTrader.ExecuteAsync(
                new CaseId(caseId),
                UserId.Create(request.TargetTraderId),
                new StateVersion(request.ExpectedVersion),
                cancellationToken));

    [HttpPost("{caseId:long}/take-over")]
    public Task<ActionResult<OwnershipResponse>> TakeOver(
        long caseId,
        ConfirmedOwnershipRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(() =>
            takeOverRfq.ExecuteAsync(
                new CaseId(caseId),
                new StateVersion(request.ExpectedVersion),
                request.Confirmed,
                cancellationToken));

    [HttpPut("{caseId:long}/working-quote/calculate")]
    public Task<ActionResult<WorkingQuoteResponse>> Calculate(
        long caseId,
        CalculateWorkingQuoteRequest request,
        CancellationToken cancellationToken) => ExecuteQuoteAsync(async () =>
    {
        if (!Enum.TryParse<CalculationDriver>(request.Driver, false, out var driver))
        {
            throw new ArgumentException($"Unknown calculation driver '{request.Driver}'.");
        }

        return await calculateWorkingQuote.ExecuteAsync(
            new CaseId(caseId),
            driver,
            request.Value,
            request.SimpleYieldSlide,
            new StateVersion(request.ExpectedCurrentVersion),
            new StateVersion(request.ExpectedWorkingQuoteVersion),
            cancellationToken);
    });

    [HttpPut("{caseId:long}/working-quote/mode")]
    public Task<ActionResult<WorkingQuoteResponse>> ChangeMode(
        long caseId,
        ChangeWorkingQuoteModeRequest request,
        CancellationToken cancellationToken) => ExecuteQuoteAsync(async () =>
    {
        if (!Enum.TryParse<WorkingQuoteMode>(request.Mode, false, out var mode))
        {
            throw new ArgumentException($"Unknown WorkingQuote mode '{request.Mode}'.");
        }

        return await changeWorkingQuoteMode.ExecuteAsync(
            new CaseId(caseId),
            mode,
            new StateVersion(request.ExpectedCurrentVersion),
            new StateVersion(request.ExpectedWorkingQuoteVersion),
            cancellationToken);
    });

    [HttpPut("{caseId:long}/working-quote/manual")]
    public Task<ActionResult<WorkingQuoteResponse>> UpdateManual(
        long caseId,
        UpdateManualWorkingQuoteRequest request,
        CancellationToken cancellationToken) => ExecuteQuoteAsync(() =>
            updateManualWorkingQuote.ExecuteAsync(
                new CaseId(caseId),
                request.Price,
                request.FinalSimpleYield,
                new StateVersion(request.ExpectedCurrentVersion),
                new StateVersion(request.ExpectedWorkingQuoteVersion),
                cancellationToken));

    [HttpPost("{caseId:long}/confirm-quote")]
    public Task<ActionResult<ConfirmQuoteResponse>> ConfirmQuote(
        long caseId,
        ConfirmQuoteRequest request,
        CancellationToken cancellationToken) => ExecuteConfirmAsync(() =>
            confirmQuote.ExecuteAsync(
                new CaseId(caseId),
                request.ExpiryMinutes,
                new StateVersion(request.ExpectedCurrentVersion),
                new StateVersion(request.ExpectedWorkingQuoteVersion),
            cancellationToken));

    [HttpPost("{caseId:long}/withdraw")]
    public Task<ActionResult<LifecycleResponse>> Withdraw(
        long caseId, OwnershipRequest request, CancellationToken cancellationToken) =>
        ExecuteLifecycleAsync(() => withdrawQuote.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedVersion), cancellationToken));

    [HttpPost("bulk-withdraw")]
    public Task<ActionResult<IReadOnlyList<LifecycleItemResponse>>> BulkWithdraw(
        BulkLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteLifecycleItemsAsync(() => bulkWithdrawQuotes.ExecuteAsync(
            request.Items.Select(item => new LifecycleItem(
                new CaseId(item.CaseId), new StateVersion(item.ExpectedCurrentVersion))).ToArray(), cancellationToken));

    private async Task<ActionResult<LifecycleResponse>> ExecuteLifecycleAsync(
        Func<Task<LifecycleResult>> action)
    {
        try { return Ok(LifecycleResponse.From(await action())); }
        catch (Exception exception) when (IsExpected(exception)) { return ToProblem(exception); }
    }

    private async Task<ActionResult<IReadOnlyList<LifecycleItemResponse>>> ExecuteLifecycleItemsAsync(
        Func<Task<IReadOnlyList<LifecycleItemResult>>> action)
    {
        try
        {
            var items = await action();
            return Ok(items.Select(LifecycleItemResponse.From).ToArray());
        }
        catch (Exception exception) when (IsExpected(exception)) { return ToProblem(exception); }
    }

    private async Task<ActionResult<OwnershipResponse>> ExecuteAsync(
        Func<Task<OwnershipResult>> action)
    {
        try
        {
            return Ok(OwnershipResponse.From(await action()));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    private async Task<ActionResult<WorkingQuoteResponse>> ExecuteQuoteAsync(
        Func<Task<WorkingQuoteResult>> action)
    {
        try
        {
            return Ok(WorkingQuoteResponse.From(await action()));
        }
        catch (CalculationFailureException exception)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Calculation failed",
                Detail = exception.Message,
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["category"] = "CalculationFailure";
            problem.Extensions["failureLogId"] = exception.FailureLogId;
            return UnprocessableEntity(problem);
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    private async Task<ActionResult<ConfirmQuoteResponse>> ExecuteConfirmAsync(
        Func<Task<ConfirmQuoteResult>> action)
    {
        try
        {
            return Ok(ConfirmQuoteResponse.From(await action()));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

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
        var code = exception switch
        {
            UnauthorizedAccessException => "Forbidden",
            KeyNotFoundException => "NotFound",
            StateVersionMismatchException or DomainRuleViolationException => "Conflict",
            DomainValidationException => "Validation",
            InvalidOperationException => "Conflict",
            _ => "Validation",
        };
        var problem = new ProblemDetails { Status = statusCode, Title = code, Detail = exception.Message };
        problem.Extensions["code"] = code;
        return StatusCode(statusCode, problem);
    }
}

public sealed record OwnershipRequest(
    [Range(1, long.MaxValue)] long ExpectedVersion);

public sealed record ConfirmedOwnershipRequest(
    [Range(1, long.MaxValue)] long ExpectedVersion,
    bool Confirmed);

public sealed record AssignTraderRequest(
    [Required, MinLength(1)] string TargetTraderId,
    [Range(1, long.MaxValue)] long ExpectedVersion);

public sealed record CalculateWorkingQuoteRequest(
    [Required, MinLength(1)] string Driver,
    decimal Value,
    decimal SimpleYieldSlide,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record ChangeWorkingQuoteModeRequest(
    [Required, MinLength(1)] string Mode,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record UpdateManualWorkingQuoteRequest(
    decimal? Price,
    decimal? FinalSimpleYield,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record ConfirmQuoteRequest(
    [Range(1, int.MaxValue)] int? ExpiryMinutes,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record LifecycleItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record BulkLifecycleRequest(
    [Required, MinLength(1)] IReadOnlyList<LifecycleItemRequest> Items);

public sealed record TraderRfqResponse(
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
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ExpiresAt,
    Guid? QuoteSeedRevisionId,
    string ContactOwnerId,
    string AssignedTraderId,
    bool Owned,
    long CurrentVersion,
    DateOnly? SettlementDate,
    decimal? Notional,
    string WorkingQuoteMode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    long WorkingQuoteVersion,
    string TraderMemo,
    long MemoVersion,
    DateTimeOffset CreatedAt)
{
    public static TraderRfqResponse From(TraderRfqListItem item) => new(
        item.CaseId.Value, item.ClientId.Value, item.ClientName, item.SecurityId.Value,
        item.SecurityJapaneseName, item.SecurityBbgDisplay, item.CategoryId.Value,
        item.RfqStatus.ToString(), item.QuoteStatus?.ToString(),
        item.QuoteRequestReason?.ToString(), item.CurrentRevisionId.Value,
        item.CurrentQuoteId?.Value, item.ClosedQuoteId?.Value, item.ConfirmedAt,
        item.ExpiresAt, item.QuoteSeedRevisionId?.Value, item.ContactOwnerId.Value,
        item.AssignedTraderId.Value, item.Owned, item.CurrentVersion.Value,
        item.SettlementDate, item.Notional, item.WorkingQuoteMode.ToString(),
        item.Calculated, item.Manual, item.WorkingQuoteVersion.Value,
        item.TraderMemo, item.MemoVersion.Value, item.CreatedAt);
}

public sealed record OwnershipResponse(
    long CaseId,
    string AssignedTraderId,
    bool Owned,
    long CurrentVersion)
{
    public static OwnershipResponse From(OwnershipResult result) => new(
        result.CaseId.Value, result.AssignedTraderId.Value, result.Owned,
        result.CurrentVersion.Value);
}

public sealed record WorkingQuoteResponse(
    long CaseId,
    Guid RevisionId,
    string Mode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    long Version,
    long CurrentVersion)
{
    public static WorkingQuoteResponse From(WorkingQuoteResult result) => new(
        result.CaseId.Value, result.RevisionId.Value, result.Mode.ToString(),
        result.Calculated, result.Manual, result.Version.Value, result.CurrentVersion.Value);
}

public sealed record ConfirmQuoteResponse(
    long CaseId,
    Guid QuoteId,
    Guid RevisionId,
    string RfqStatus,
    string QuoteStatus,
    string Mode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    DateTimeOffset ConfirmedAt,
    int? ExpiryMinutes,
    DateTimeOffset? ExpiresAt,
    long CurrentVersion)
{
    public static ConfirmQuoteResponse From(ConfirmQuoteResult result) => new(
        result.CaseId.Value, result.QuoteId.Value, result.RevisionId.Value,
        result.RfqStatus.ToString(), result.QuoteStatus.ToString(), result.Mode.ToString(),
        result.Calculated, result.Manual, result.ConfirmedAt, result.ExpiryMinutes,
        result.ExpiresAt, result.CurrentVersion.Value);
}

public sealed record LifecycleItemResponse(long CaseId, string Result, string? Error)
{
    public static LifecycleItemResponse From(LifecycleItemResult result) =>
        new(result.CaseId.Value, result.Result, result.Error);
}
