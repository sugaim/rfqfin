using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/trader/rfqs")]
public sealed class TraderRfqsController(
    GetActiveTraderRfqs getActiveTraderRfqs,
    PickUpRfq pickUpRfq,
    ReleaseRfq releaseRfq,
    AssignTrader assignTrader,
    TakeOverRfq takeOverRfq) : ControllerBase
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
