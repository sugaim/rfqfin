using Rfq.Domain;

namespace Rfq.Application;

public sealed record RevisionHistoryItem(
    RevisionId RevisionId,
    RevisionStatus Status,
    decimal? Notional,
    DateOnly? SettlementDate,
    string Message,
    StateVersion Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    RevisionId? CopiedFromRevisionId,
    RevisionId? QuoteSeedRevisionId);
