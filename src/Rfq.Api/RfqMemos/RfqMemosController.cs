using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqMemos;

[ApiController]
[Route("api/rfqs/{caseId:long}/memos")]
public sealed class RfqMemosController(
    UpdateSalesMemo updateSales, UpdateTraderMemo updateTrader) : ControllerBase
{
    [HttpPut("sales")]
    public async Task<SalesMemoResponse> Sales(
        long caseId,
        UpdateMemoRequest request,
        CancellationToken token)
    {
        MemoResult value = await updateSales.ExecuteAsync(
            new CaseId(caseId),
            request.Memo,
            new StateVersion(request.ExpectedVersion),
            token);
        return new(value.CaseId.Value, value.Memo, value.Version.Value);
    }

    [HttpPut("trader")]
    public async Task<TraderMemoResponse> Trader(
        long caseId,
        UpdateMemoRequest request,
        CancellationToken token)
    {
        MemoResult value = await updateTrader.ExecuteAsync(
            new CaseId(caseId),
            request.Memo,
            new StateVersion(request.ExpectedVersion),
            token);
        return new(value.CaseId.Value, value.Memo, value.Version.Value);
    }
}

public sealed record UpdateMemoRequest(
    string? Memo,
    [Range(1, long.MaxValue)] long ExpectedVersion);

public sealed record SalesMemoResponse(long CaseId, string Memo, long Version);
public sealed record TraderMemoResponse(long CaseId, string Memo, long Version);
