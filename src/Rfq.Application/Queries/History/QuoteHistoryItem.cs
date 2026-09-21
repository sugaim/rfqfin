using Rfq.Domain;

namespace Rfq.Application;

public sealed record QuoteHistoryItem(Guid QuoteId, Guid RevisionId, string Mode,
    DateTimeOffset ConfirmedAt, DateTimeOffset? ExpiresAt, string RequestReason);
