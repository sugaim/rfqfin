using Rfq.Domain;

namespace Rfq.Application;

public sealed record RfqTransition(
    RfqTransitionKind Kind,
    long CaseId,
    string PerformedBy,
    DateTimeOffset OccurredAt,
    Guid? QuoteId = null,
    string? From = null,
    string? To = null,
    string? Reason = null);
