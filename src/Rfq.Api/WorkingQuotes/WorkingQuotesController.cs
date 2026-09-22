using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Api.RfqQuotes;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.WorkingQuotes;

[ApiController]
[Route("api/rfqs/{caseId:long}/working-quote")]
public sealed class WorkingQuotesController(
    CalculateWorkingQuote calculate,
    ChangeWorkingQuoteMode changeMode,
    UpdateManualWorkingQuote updateManual) : ControllerBase
{
    [HttpPut("calculate")]
    public async Task<WorkingQuoteResponse> Calculate(
        long caseId,
        CalculateWorkingQuoteRequest request,
        CancellationToken cancellationToken) =>
        WorkingQuoteApiMapper.ToApi(await calculate.ExecuteAsync(
            new CaseId(caseId),
            QuoteApiMapper.ToDomain(request.Driver),
            request.Value,
            request.SimpleYieldSlide,
            new StateVersion(request.ExpectedCurrentVersion),
            new StateVersion(request.ExpectedWorkingQuoteVersion),
            cancellationToken));

    [HttpPut("mode")]
    public async Task<WorkingQuoteResponse> Mode(
        long caseId,
        ChangeWorkingQuoteModeRequest request,
        CancellationToken cancellationToken) =>
        WorkingQuoteApiMapper.ToApi(await changeMode.ExecuteAsync(
            new CaseId(caseId),
            QuoteApiMapper.ToDomain(request.Mode),
            new StateVersion(request.ExpectedCurrentVersion),
            new StateVersion(request.ExpectedWorkingQuoteVersion),
            cancellationToken));

    [HttpPut("manual")]
    public async Task<WorkingQuoteResponse> Manual(
        long caseId,
        UpdateManualWorkingQuoteRequest request,
        CancellationToken cancellationToken) =>
        WorkingQuoteApiMapper.ToApi(await updateManual.ExecuteAsync(
            new CaseId(caseId),
            request.Price,
            request.FinalSimpleYield,
            new StateVersion(request.ExpectedCurrentVersion),
            new StateVersion(request.ExpectedWorkingQuoteVersion),
            cancellationToken));
}

public sealed record CalculateWorkingQuoteRequest(
    [Required] CalculationDriverValue Driver,
    decimal Value,
    decimal SimpleYieldSlide,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record ChangeWorkingQuoteModeRequest(
    [Required] QuoteMode Mode,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record UpdateManualWorkingQuoteRequest(
    decimal? Price,
    decimal? FinalSimpleYield,
    [Range(1, long.MaxValue)] long ExpectedCurrentVersion,
    [Range(1, long.MaxValue)] long ExpectedWorkingQuoteVersion);

public sealed record WorkingQuoteResponse(
    long CaseId,
    Guid RevisionId,
    QuoteMode Mode,
    CalculatedQuoteResponse? Calculated,
    ManualQuoteResponse? Manual,
    long Version,
    long CurrentVersion);

public static class WorkingQuoteApiMapper
{
    public static WorkingQuoteResponse ToApi(WorkingQuoteResult value) => new(
        value.CaseId.Value,
        value.RevisionId.Value,
        QuoteApiMapper.ToApi(value.Mode),
        QuoteApiMapper.ToApi(value.Calculated),
        QuoteApiMapper.ToApi(value.Manual),
        value.Version.Value,
        value.CurrentVersion.Value);
}
