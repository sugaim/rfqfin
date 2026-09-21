using Rfq.Domain;

namespace Rfq.Application;

public sealed record UpdateInitialDraftCommand(
    long CaseId,
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    string? AssignedTraderId,
    long ExpectedVersion);
