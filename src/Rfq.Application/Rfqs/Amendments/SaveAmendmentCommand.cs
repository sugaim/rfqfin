using Rfq.Domain;

namespace Rfq.Application;

public sealed record SaveAmendmentCommand(
    long CaseId,
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    long ExpectedCurrentVersion,
    long? ExpectedDraftVersion);
