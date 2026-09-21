using Rfq.Domain;

namespace Rfq.Application;

public sealed record PersistedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    string Kind,
    string Type,
    string PayloadJson);

public sealed record RfqTransition(
    RfqTransitionKind Kind,
    CaseId CaseId,
    UserId PerformedBy,
    DateTimeOffset OccurredAt,
    QuoteId? QuoteId = null,
    string? From = null,
    string? To = null,
    string? Reason = null);

public enum RfqTransitionKind
{
    ClosedHit,
    ClosedAway,
    OutcomeCorrected,
    ContactOwnerChanged,
    RevisionConfirmed,
    Cancelled,
    Reopened,
    PickedUp,
    Released,
    AssignedTraderChanged,
    TakenOver,
}

public sealed record QuoteTransition(
    QuoteTransitionKind Kind,
    QuoteId QuoteId,
    UserId PerformedBy,
    DateTimeOffset OccurredAt);

public enum QuoteTransitionKind
{
    Confirmed,
    Presented,
    Unpresented,
    Withdrawn,
    Expired,
}
