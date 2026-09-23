using Rfq.Domain;

namespace Rfq.Application;

public sealed record SaveAmendmentCommand(
    CaseId CaseId,
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    StateVersion ExpectedCurrentVersion,
    StateVersion? ExpectedDraftVersion);

public sealed record StartAmendmentCommand(
    CaseId CaseId,
    StateVersion ExpectedCurrentVersion);

public sealed record AmendmentResult(
    CaseId CaseId,
    RevisionId CurrentRevisionId,
    RevisionId? DraftRevisionId,
    StateVersion CurrentVersion,
    StateVersion? DraftVersion,
    decimal? DraftNotional,
    DateOnly? DraftSettlementDate,
    string? DraftSalesAndTradingMessage,
    RfqStatus RfqStatus,
    QuoteStatus? QuoteStatus,
    QuoteRequestReason? QuoteRequestReason);

public sealed record AmendmentItem(
    CaseId CaseId,
    StateVersion ExpectedCurrentVersion,
    StateVersion ExpectedDraftVersion);
