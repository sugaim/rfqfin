using Rfq.Domain;

namespace Rfq.Application;

public sealed record TraderRfqListItem(
    long CaseId,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityJapaneseName,
    string SecurityBbgDisplay,
    string CategoryId,
    string RfqStatus,
    string? QuoteStatus,
    string? QuoteRequestReason,
    Guid CurrentRevisionId,
    Guid? CurrentQuoteId,
    Guid? ClosedQuoteId,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ExpiresAt,
    Guid? QuoteSeedRevisionId,
    string ContactOwnerId,
    string AssignedTraderId,
    bool Owned,
    long CurrentVersion,
    DateOnly? SettlementDate,
    decimal? Notional,
    string WorkingQuoteMode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    long WorkingQuoteVersion,
    string TraderMemo,
    long MemoVersion,
    DateTimeOffset CreatedAt);
