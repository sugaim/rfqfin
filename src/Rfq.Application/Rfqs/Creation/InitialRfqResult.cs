using Rfq.Domain;

namespace Rfq.Application;

public sealed record InitialRfqResult(
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
    DateTimeOffset CreatedAt)
{
    public static InitialRfqResult From(RfqCase rfqCase) => new(
        rfqCase.CaseId.Value,
        rfqCase.CurrentRevision.RevisionId.Value,
        rfqCase.Status.ToString(),
        rfqCase.CurrentRevision.Status.ToString(),
        rfqCase.QuoteStatus?.ToString(),
        rfqCase.QuoteRequestReason?.ToString(),
        rfqCase.CategorySnapshot.Value,
        rfqCase.ContactOwnerId.Value,
        rfqCase.AssignedTraderId.Value,
        rfqCase.CurrentRevision.Notional,
        rfqCase.CurrentRevision.SettlementDate,
        rfqCase.CurrentRevision.StandardSettlementDate,
        rfqCase.CurrentRevision.SalesAndTradingMessage,
        rfqCase.CurrentRevision.Version.Value,
        rfqCase.CreatedAt);
}
