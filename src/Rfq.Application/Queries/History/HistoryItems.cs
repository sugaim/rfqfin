using Rfq.Domain;

namespace Rfq.Application;

public sealed record RevisionHistoryItem(RevisionId RevisionId, RevisionStatus Status, decimal? Notional,
    DateOnly? SettlementDate, string Message, StateVersion Version, DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt, RevisionId? CopiedFromRevisionId, RevisionId? QuoteSeedRevisionId);

public sealed record QuoteHistoryItem(QuoteId QuoteId, RevisionId RevisionId, WorkingQuoteMode Mode,
    DateTimeOffset ConfirmedAt, DateTimeOffset? ExpiresAt, QuoteRequestReason RequestReason);
