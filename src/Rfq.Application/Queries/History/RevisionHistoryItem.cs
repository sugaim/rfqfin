using Rfq.Domain;

namespace Rfq.Application;

public sealed record RevisionHistoryItem(Guid RevisionId, string Status, decimal? Notional,
    DateOnly? SettlementDate, string Message, long Version, DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt, Guid? CopiedFromRevisionId, Guid? QuoteSeedRevisionId);
