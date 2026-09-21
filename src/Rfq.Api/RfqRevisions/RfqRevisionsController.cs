using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.RfqRevisions;

[ApiController]
[Route("api/rfqs/{caseId:long}/revisions")]
public sealed class RfqRevisionsController(IRfqRevisionQueries revisions) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<RevisionResponse>> Get(long caseId,
        CancellationToken token) => (await revisions.GetAsync(new CaseId(caseId), token))
        .Select(RevisionApiMapper.ToApi).ToArray();
}
public enum RevisionStatusValue { Draft, Confirmed, Superseded, Discarded }
public sealed record RevisionResponse(Guid RevisionId, RevisionStatusValue Status,
    decimal? Notional, DateOnly? SettlementDate, string Message, long Version,
    DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt,
    Guid? CopiedFromRevisionId, Guid? QuoteSeedRevisionId);
public static class RevisionApiMapper
{
    public static RevisionResponse ToApi(RevisionHistoryItem value) => new(
        value.RevisionId.Value, Enum.Parse<RevisionStatusValue>(value.Status.ToString()),
        value.Notional, value.SettlementDate, value.Message, value.Version.Value,
        value.CreatedAt, value.ConfirmedAt, value.CopiedFromRevisionId?.Value,
        value.QuoteSeedRevisionId?.Value);
}
