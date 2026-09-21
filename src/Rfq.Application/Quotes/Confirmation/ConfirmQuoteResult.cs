using Rfq.Domain;

namespace Rfq.Application;

public sealed record ConfirmQuoteResult(
    long CaseId,
    Guid QuoteId,
    Guid RevisionId,
    string RfqStatus,
    string QuoteStatus,
    string Mode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    DateTimeOffset ConfirmedAt,
    int? ExpiryMinutes,
    DateTimeOffset? ExpiresAt,
    long CurrentVersion);
