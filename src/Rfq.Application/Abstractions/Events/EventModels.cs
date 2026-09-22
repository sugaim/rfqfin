using Rfq.Domain;

namespace Rfq.Application;

public abstract record EventFeedItem(long EventId, DateTimeOffset OccurredAt, UserId? ActorUserId);

public abstract record RfqEvent(long EventId, DateTimeOffset OccurredAt, UserId? ActorUserId, CaseId CaseId)
    : EventFeedItem(EventId, OccurredAt, ActorUserId);

public abstract record QuoteEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId) : EventFeedItem(EventId, OccurredAt, ActorUserId);

public sealed record RfqClosedHitEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqClosedAwayEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqOutcomeCorrectedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId,
    RfqStatus From,
    RfqStatus To,
    string? Reason)
    : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqContactOwnerChangedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    UserId From,
    UserId To) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqRevisionConfirmedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    RevisionId? From,
    RevisionId To) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqCancelledEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqReopenedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqPickedUpEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    UserId AssignedTraderId) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqReleasedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    UserId AssignedTraderId) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqAssignedTraderChangedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    UserId From,
    UserId To) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqTakenOverEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    UserId From,
    UserId To) : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record QuoteConfirmedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId) : QuoteEvent(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

public sealed record QuotePresentedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId) : QuoteEvent(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

public sealed record QuoteUnpresentedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId) : QuoteEvent(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

public sealed record QuoteWithdrawnEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId) : QuoteEvent(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

public sealed record QuoteExpiredEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId) : QuoteEvent(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

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
    Expired
}
