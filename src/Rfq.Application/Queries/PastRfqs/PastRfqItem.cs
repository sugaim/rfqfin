using Rfq.Domain;

namespace Rfq.Application;

public sealed record PastRfqItem(
    long CaseId, DateTimeOffset CreatedAt, string ClientId, string ClientName,
    string SecurityId, string SecurityName, string CategoryId, string Status,
    string? QuoteStatus, string ContactOwnerId, string SalesId, string AssignedTraderId,
    decimal? Notional, DateOnly? SettlementDate);
