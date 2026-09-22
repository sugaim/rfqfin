using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqLifecycle;

[ApiController]
[Route("api/rfqs")]
public sealed class RfqLifecycleController(
    PresentQuote present,
    UnpresentQuote unpresent,
    CloseHitRfq closeHit,
    CloseAwayRfq closeAway,
    CorrectOutcomeToHit correctToHit,
    CorrectOutcomeToAway correctToAway,
    CancelRfq cancel,
    ReopenRfq reopen,
    BulkPresentQuotes bulkPresent,
    BulkUnpresentQuotes bulkUnpresent,
    BulkCloseAwayRfqs bulkCloseAway,
    BulkCancelRfqs bulkCancel) : ControllerBase
{
    [HttpPost("{caseId:long}/present")]
    public async Task<PresentationResponse> Present(
        long caseId,
        LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await present.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("{caseId:long}/unpresent")]
    public async Task<PresentationResponse> Unpresent(
        long caseId,
        LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await unpresent.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("{caseId:long}/close/hit")]
    public async Task<CloseResponse> CloseHit(
        long caseId,
        LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await closeHit.ExecuteAsync(
            new CaseId(caseId),
            new StateVersion(request.ExpectedCurrentVersion),
            token));

    [HttpPost("{caseId:long}/close/away")]
    public async Task<CloseResponse> CloseAway(
        long caseId,
        LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi((await closeAway.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token)).Rfq);

    [HttpPost("{caseId:long}/outcome/correct-to-hit")]
    public async Task<CloseResponse> CorrectToHit(
        long caseId,
        CorrectOutcomeRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await correctToHit.ExecuteAsync(
            new CaseId(caseId),
            request.Reason,
            new StateVersion(request.ExpectedCurrentVersion),
            token));

    [HttpPost("{caseId:long}/outcome/correct-to-away")]
    public async Task<CloseResponse> CorrectToAway(
        long caseId,
        CorrectOutcomeRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await correctToAway.ExecuteAsync(
            new CaseId(caseId),
            request.Reason,
            new StateVersion(request.ExpectedCurrentVersion),
            token));

    [HttpPost("{caseId:long}/cancel")]
    public async Task<LifecycleResponse> Cancel(
        long caseId,
        LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await cancel.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("{caseId:long}/reopen")]
    public async Task<LifecycleResponse> Reopen(
        long caseId,
        LifecycleRequest request,
        CancellationToken token) => LifecycleApiMapper.ToApi(await reopen.ExecuteAsync(
            new CaseId(caseId), new StateVersion(request.ExpectedCurrentVersion), token));

    [HttpPost("bulk-present")]
    public Task<IReadOnlyList<BulkItemResponse>> BulkPresent(
        BulkLifecycleRequest request,
        CancellationToken token) => ExecuteBulk(request, bulkPresent.ExecuteAsync, token);

    [HttpPost("bulk-unpresent")]
    public Task<IReadOnlyList<BulkItemResponse>> BulkUnpresent(
        BulkLifecycleRequest request,
        CancellationToken token) => ExecuteBulk(request, bulkUnpresent.ExecuteAsync, token);

    [HttpPost("bulk-close-away")]
    public Task<IReadOnlyList<BulkItemResponse>> BulkCloseAway(
        BulkLifecycleRequest request,
        CancellationToken token) => ExecuteBulk(request, bulkCloseAway.ExecuteAsync, token);

    [HttpPost("bulk-cancel")]
    public Task<IReadOnlyList<BulkItemResponse>> BulkCancel(
        BulkLifecycleRequest request,
        CancellationToken token) => ExecuteBulk(request, bulkCancel.ExecuteAsync, token);

    private static async Task<IReadOnlyList<BulkItemResponse>> ExecuteBulk(
        BulkLifecycleRequest request,
        Func<IReadOnlyList<LifecycleItem>, CancellationToken, Task<IReadOnlyList<BulkItemResult>>> action,
        CancellationToken token) => [.. (await action(
            [.. request.Items.Select(item => new LifecycleItem(
                new CaseId(item.CaseId), new StateVersion(item.ExpectedCurrentVersion)))],
            token))
            .Select(BulkApiMapper.ToApi)];
}

public enum RfqStatusValue
{
    Draft,
    Active,
    Presented,
    Cancelled,
    Hit,
    Away
}

public enum QuoteStatusValue
{
    Requested,
    Quoted
}

public enum QuoteRequestReasonValue
{
    Initial,
    Revised,
    Reopened,
    Expired,
    Withdrawn
}

public sealed record LifecycleRequest([Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record CorrectOutcomeRequest(
    string? Reason,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record BulkLifecycleItemRequest(
    [Range(1, long.MaxValue)] long CaseId,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion);

public sealed record BulkLifecycleRequest(
    [Required, MinLength(1)] IReadOnlyList<BulkLifecycleItemRequest> Items);

public sealed record PresentationResponse(
    long CaseId,
    Guid QuoteId,
    RfqStatusValue RfqStatus,
    QuoteStatusValue QuoteStatus,
    long CurrentVersion);

public sealed record CloseResponse(
    long CaseId,
    RfqStatusValue RfqStatus,
    Guid ClosedQuoteId,
    bool Owned,
    long CurrentVersion);

public sealed record LifecycleResponse(
    long CaseId,
    RfqStatusValue RfqStatus,
    QuoteStatusValue? QuoteStatus,
    QuoteRequestReasonValue? QuoteRequestReason,
    long CurrentVersion);

public static class LifecycleApiMapper
{
    public static PresentationResponse ToApi(PresentationResult value) => new(
        value.CaseId.Value,
        value.QuoteId.Value,
        Map<RfqStatusValue>(value.RfqStatus),
        Map<QuoteStatusValue>(value.QuoteStatus),
        value.CurrentVersion.Value);

    public static CloseResponse ToApi(CloseRfqResult value) => new(
        value.CaseId.Value,
        Map<RfqStatusValue>(value.RfqStatus),
        value.ClosedQuoteId.Value,
        value.Owned,
        value.CurrentVersion.Value);

    public static LifecycleResponse ToApi(LifecycleResult value) => new(
        value.CaseId.Value,
        Map<RfqStatusValue>(value.RfqStatus),
        MapNullable<QuoteStatusValue>(value.QuoteStatus),
        MapNullable<QuoteRequestReasonValue>(value.QuoteRequestReason),
        value.CurrentVersion.Value);

    private static T Map<T>(Enum value) where T : struct, Enum => Enum.Parse<T>(value.ToString());

    private static T? MapNullable<T>(Enum? value) where T : struct, Enum =>
        value is null ? null : Map<T>(value);
}
