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
    ConfirmQuote confirmQuote) : ControllerBase
{
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<TraderRfqListItem>>> GetActive(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await getActiveTraderRfqs.ExecuteAsync(cancellationToken));
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    [HttpPost("{caseId:long}/pick-up")]
    public Task<ActionResult<OwnershipResult>> PickUp(
        long caseId,
        ConfirmedOwnershipRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(() =>
            pickUpRfq.ExecuteAsync(
                caseId,
                request.ExpectedVersion,
                request.Confirmed,
                cancellationToken));

    [HttpPost("{caseId:long}/release")]
    public Task<ActionResult<OwnershipResult>> Release(
        long caseId,
        OwnershipRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(() =>
            releaseRfq.ExecuteAsync(caseId, request.ExpectedVersion, cancellationToken));

    [HttpPost("{caseId:long}/assign")]
    public Task<ActionResult<OwnershipResult>> Assign(
        long caseId,
        AssignTraderRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(() =>
            assignTrader.ExecuteAsync(
                caseId,
                request.TargetTraderId,
                request.ExpectedVersion,
                cancellationToken));

    [HttpPost("{caseId:long}/take-over")]
    public Task<ActionResult<OwnershipResult>> TakeOver(
        long caseId,
        ConfirmedOwnershipRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(() =>
            takeOverRfq.ExecuteAsync(
                caseId,
                request.ExpectedVersion,
                request.Confirmed,
                cancellationToken));

    [HttpPut("{caseId:long}/working-quote/calculate")]
    public Task<ActionResult<WorkingQuoteResult>> Calculate(
        long caseId,
        CalculateWorkingQuoteRequest request,
        CancellationToken cancellationToken) => ExecuteQuoteAsync(async () =>
    {
        if (!Enum.TryParse<CalculationDriver>(request.Driver, false, out var driver))
        {
            throw new ArgumentException($"Unknown calculation driver '{request.Driver}'.");
        }

        return await calculateWorkingQuote.ExecuteAsync(
            caseId,
            driver,
            request.Value,
            request.SimpleYieldSlide,
            request.ExpectedCurrentVersion,
            request.ExpectedWorkingQuoteVersion,
            cancellationToken);
    });

    [HttpPut("{caseId:long}/working-quote/mode")]
    public Task<ActionResult<WorkingQuoteResult>> ChangeMode(
        long caseId,
        ChangeWorkingQuoteModeRequest request,
        CancellationToken cancellationToken) => ExecuteQuoteAsync(async () =>
    {
        if (!Enum.TryParse<WorkingQuoteMode>(request.Mode, false, out var mode))
        {
            throw new ArgumentException($"Unknown WorkingQuote mode '{request.Mode}'.");
        }

        return await changeWorkingQuoteMode.ExecuteAsync(
            caseId,
            mode,
            request.ExpectedCurrentVersion,
            request.ExpectedWorkingQuoteVersion,
            cancellationToken);
    });

    [HttpPut("{caseId:long}/working-quote/manual")]
    public Task<ActionResult<WorkingQuoteResult>> UpdateManual(
        long caseId,
        UpdateManualWorkingQuoteRequest request,
        CancellationToken cancellationToken) => ExecuteQuoteAsync(() =>
            updateManualWorkingQuote.ExecuteAsync(
                caseId,
                request.Price,
                request.FinalSimpleYield,
                request.ExpectedCurrentVersion,
                request.ExpectedWorkingQuoteVersion,
                cancellationToken));

    [HttpPost("{caseId:long}/confirm-quote")]
    public Task<ActionResult<ConfirmQuoteResult>> ConfirmQuote(
        long caseId,
        ConfirmQuoteRequest request,
        CancellationToken cancellationToken) => ExecuteConfirmAsync(() =>
            confirmQuote.ExecuteAsync(
                caseId,
                request.ExpiryMinutes,
                request.ExpectedCurrentVersion,
                request.ExpectedWorkingQuoteVersion,
                cancellationToken));

    private async Task<ActionResult<OwnershipResult>> ExecuteAsync(
        Func<Task<OwnershipResult>> action)
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

    private async Task<ActionResult<WorkingQuoteResult>> ExecuteQuoteAsync(
        Func<Task<WorkingQuoteResult>> action)
    {
        try
        {
            return Ok(await action());
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
            problem.Extensions["failureLogId"] = exception.FailureLogId;
            return UnprocessableEntity(problem);
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ToProblem(exception);
        }
    }

    private async Task<ActionResult<ConfirmQuoteResult>> ExecuteConfirmAsync(
        Func<Task<ConfirmQuoteResult>> action)
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
