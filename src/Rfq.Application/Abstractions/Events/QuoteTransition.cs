using Rfq.Domain;

namespace Rfq.Application;

public sealed record QuoteTransition(
    QuoteTransitionKind Kind,
    Guid QuoteId,
    string PerformedBy,
    DateTimeOffset OccurredAt);
