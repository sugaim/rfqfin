using Rfq.Domain;

namespace Rfq.Application;

public sealed record CreateDraftCommand(
    ClientId ClientId,
    SecurityId SecurityId,
    decimal? Notional,
    DateOnly SettlementDate,
    DateOnly StandardSettlementDate,
    string SalesAndTradingMessage,
    UserId AssignedTraderId);

public sealed record UpdateInitialDraftCommand(
    CaseId CaseId,
    decimal? Notional,
    DateOnly SettlementDate,
    DateOnly StandardSettlementDate,
    string SalesAndTradingMessage,
    UserId AssignedTraderId,
    StateVersion ExpectedVersion);

public sealed record InitialRfqResult(
    CaseId CaseId,
    RevisionId RevisionId,
    RfqStatus RfqStatus,
    RevisionStatus RevisionStatus,
    QuoteStatus? QuoteStatus,
    QuoteRequestReason? QuoteRequestReason,
    CategoryId CategoryId,
    UserId ContactOwnerId,
    UserId AssignedTraderId,
    decimal? Notional,
    DateOnly? SettlementDate,
    DateOnly StandardSettlementDate,
    string SalesAndTradingMessage,
    StateVersion Version,
    DateTimeOffset CreatedAt)
{
    public static InitialRfqResult From(RfqCase rfqCase) => new(
        rfqCase.CaseId,
        rfqCase.CurrentRevision.RevisionId,
        rfqCase.Status,
        rfqCase.CurrentRevision.Status,
        rfqCase.QuoteStatus,
        rfqCase.QuoteRequestReason,
        rfqCase.CategorySnapshot,
        rfqCase.ContactOwnerId,
        rfqCase.AssignedTraderId,
        rfqCase.CurrentRevision.Notional,
        rfqCase.CurrentRevision.SettlementDate,
        rfqCase.CurrentRevision.StandardSettlementDate,
        rfqCase.CurrentRevision.SalesAndTradingMessage,
        rfqCase.CurrentRevision.Version,
        rfqCase.CreatedAt);
}

public sealed record RfqCreationContext(
    CategoryId CategoryId,
    string CategoryName,
    UserId DefaultAssignedTraderId,
    string DefaultAssignedTraderName,
    DateOnly StandardSettlementDate);
