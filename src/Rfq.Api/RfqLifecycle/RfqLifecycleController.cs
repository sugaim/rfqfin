using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqLifecycle;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqLifecycleController(
    PresentQuote present, UnpresentQuote unpresent, CloseRfq close,
    CorrectRfqOutcome correct, CancelRfq cancel, ReopenRfq reopen,
    BulkCloseRfqs bulkClose) : ControllerBase
{
    [HttpPost("{caseId:long}/present")]
    public async Task<PresentationResponse> Present(long caseId, LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await present.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("{caseId:long}/unpresent")]
    public async Task<PresentationResponse> Unpresent(long caseId, LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await unpresent.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("{caseId:long}/close")]
    public async Task<CloseResponse> Close(long caseId, CloseRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await close.ExecuteAsync(
            new CaseId(caseId), LifecycleApiMapper.ToDomain(request.Outcome),
            new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("{caseId:long}/correct-outcome")]
    public async Task<CloseResponse> Correct(long caseId, CorrectOutcomeRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await correct.ExecuteAsync(
            new CaseId(caseId), LifecycleApiMapper.ToDomain(request.Outcome), request.Reason,
            new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("{caseId:long}/cancel")]
    public async Task<LifecycleResponse> Cancel(long caseId, LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await cancel.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("{caseId:long}/reopen")]
    public async Task<LifecycleResponse> Reopen(long caseId, LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await reopen.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("bulk-close")]
    public async Task<IReadOnlyList<BulkCloseResponse>> BulkClose(BulkCloseRequest request,
        CancellationToken token) => (await bulkClose.ExecuteAsync(request.Items.Select(item =>
            new BulkCloseItem(new CaseId(item.CaseId),
                new StateVersion(item.ExpectedCurrentVersion))).ToArray(),
            LifecycleApiMapper.ToDomain(request.Outcome), token))
            .Select(LifecycleApiMapper.ToApi).ToArray();
}

public enum RfqOutcome { Hit, Away }
public enum RfqStatusValue { Draft, Active, Presented, Cancelled, Hit, Away }
public enum QuoteStatusValue { Requested, Quoted }
public enum QuoteRequestReasonValue { Initial, Revised, Reopened, Expired, Withdrawn }
public sealed record LifecycleRequest([Range(1, long.MaxValue)] long ExpectedCurrentVersion);
public sealed record CloseRequest([Required] RfqOutcome Outcome,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);
public sealed record CorrectOutcomeRequest([Required] RfqOutcome Outcome, string? Reason,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);
public sealed record BulkCloseItemRequest([Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);
public sealed record BulkCloseRequest([Required] RfqOutcome Outcome,
    [Required, MinLength(1)] IReadOnlyList<BulkCloseItemRequest> Items);
public sealed record PresentationResponse(long CaseId, Guid QuoteId,
    RfqStatusValue RfqStatus, QuoteStatusValue QuoteStatus, long CurrentVersion);
public sealed record CloseResponse(long CaseId, RfqStatusValue RfqStatus,
    Guid ClosedQuoteId, bool Owned, long CurrentVersion);
public sealed record LifecycleResponse(long CaseId, RfqStatusValue RfqStatus,
    QuoteStatusValue? QuoteStatus, QuoteRequestReasonValue? QuoteRequestReason,
    long CurrentVersion);
public sealed record BulkCloseResponse(long CaseId, string Result,
    RfqStatusValue? RfqStatus, string? Error);

public static class LifecycleApiMapper
{
    public static RfqStatus ToDomain(RfqOutcome value) => value switch
    {
        RfqOutcome.Hit => RfqStatus.Hit,
        RfqOutcome.Away => RfqStatus.Away,
        _ => throw new ArgumentException("Unknown RFQ outcome.", nameof(value)),
    };
    public static PresentationResponse ToApi(PresentationResult value) => new(
        value.CaseId.Value, value.QuoteId.Value, Map<RfqStatusValue>(value.RfqStatus),
        Map<QuoteStatusValue>(value.QuoteStatus), value.CurrentVersion.Value);
    public static CloseResponse ToApi(CloseRfqResult value) => new(value.CaseId.Value,
        Map<RfqStatusValue>(value.RfqStatus), value.ClosedQuoteId.Value, value.Owned,
        value.CurrentVersion.Value);
    public static LifecycleResponse ToApi(LifecycleResult value) => new(value.CaseId.Value,
        Map<RfqStatusValue>(value.RfqStatus), MapNullable<QuoteStatusValue>(value.QuoteStatus),
        MapNullable<QuoteRequestReasonValue>(value.QuoteRequestReason), value.CurrentVersion.Value);
    public static BulkCloseResponse ToApi(BulkCloseItemResult value) => new(value.CaseId.Value,
        value.Result, value.RfqStatus is null ? null : Map<RfqStatusValue>(value.RfqStatus.Value),
        value.Error);
    private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());
    private static T? MapNullable<T>(Enum? value) where T : struct, Enum =>
        value is null ? null : Map<T>(value);
}
