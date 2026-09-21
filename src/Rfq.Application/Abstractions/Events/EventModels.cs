using Rfq.Domain;

namespace Rfq.Application;

public abstract record PersistedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    string Type,
    string PayloadJson)
{
    public abstract PersistedEventKind Kind { get; }
}

public sealed record PersistedRfqEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    string Type,
    string PayloadJson)
    : PersistedEvent(EventId, OccurredAt, ActorUserId, CaseId, Type, PayloadJson)
{
    public override PersistedEventKind Kind => PersistedEventKind.Rfq;
}

public sealed record PersistedQuoteEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId,
    string Type,
    string PayloadJson)
    : PersistedEvent(EventId, OccurredAt, ActorUserId, CaseId, Type, PayloadJson)
{
    public override PersistedEventKind Kind => PersistedEventKind.Quote;
}

public enum PersistedEventKind
{
    Rfq,
    Quote,
}

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
