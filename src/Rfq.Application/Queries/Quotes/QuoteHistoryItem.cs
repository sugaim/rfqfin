using Rfq.Domain;

namespace Rfq.Application;

public sealed record QuoteHistoryItem(
    QuoteId QuoteId,
    RevisionId RevisionId,
    WorkingQuoteMode Mode,
    DateTimeOffset ConfirmedAt,
    DateTimeOffset? ExpiresAt,
    QuoteRequestReason RequestReason);
