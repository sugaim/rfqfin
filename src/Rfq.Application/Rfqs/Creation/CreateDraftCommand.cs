using Rfq.Domain;

namespace Rfq.Application;

public sealed record CreateDraftCommand(
    string ClientId,
    string SecurityId,
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    string? AssignedTraderId);
