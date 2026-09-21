using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class PersistedEventSink : IQuoteEventSink, IRfqEventSink
{
    private readonly List<PendingEvent> pending = [];
    internal IReadOnlyList<PendingEvent> Pending => pending;

    public void Record(QuoteTransition transition) => pending.Add(transition.Kind switch
    {
        QuoteTransitionKind.Confirmed => new PendingQuoteConfirmedEvent(
            transition.QuoteId, transition.PerformedBy, transition.OccurredAt),
        QuoteTransitionKind.Presented => new PendingQuotePresentedEvent(
            transition.QuoteId, transition.PerformedBy, transition.OccurredAt),
        QuoteTransitionKind.Unpresented => new PendingQuoteUnpresentedEvent(
            transition.QuoteId, transition.PerformedBy, transition.OccurredAt),
        QuoteTransitionKind.Withdrawn => new PendingQuoteWithdrawnEvent(
            transition.QuoteId, transition.PerformedBy, transition.OccurredAt),
        QuoteTransitionKind.Expired => new PendingQuoteExpiredEvent(
            transition.QuoteId, transition.PerformedBy, transition.OccurredAt),
        _ => throw new DomainInvariantException(
            $"Unsupported Quote transition kind '{transition.Kind}'."),
    });

    public void Record(RfqTransition transition) => pending.Add(transition.Kind switch
    {
        RfqTransitionKind.ClosedHit => new PendingRfqClosedHitEvent(
            transition.CaseId, Required(transition.QuoteId, "ClosedHit QuoteId"),
            transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.ClosedAway => new PendingRfqClosedAwayEvent(
            transition.CaseId, Required(transition.QuoteId, "ClosedAway QuoteId"),
            transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.OutcomeCorrected => new PendingRfqOutcomeCorrectedEvent(
            transition.CaseId, Required(transition.QuoteId, "OutcomeCorrected QuoteId"),
            ParseStatus(transition.From, "OutcomeCorrected From"),
            ParseStatus(transition.To, "OutcomeCorrected To"), transition.Reason,
            transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.ContactOwnerChanged => new PendingRfqContactOwnerChangedEvent(
            transition.CaseId, ParseUser(transition.From, "ContactOwnerChanged From"),
            ParseUser(transition.To, "ContactOwnerChanged To"),
            transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.RevisionConfirmed => new PendingRfqRevisionConfirmedEvent(
            transition.CaseId, ParseOptionalRevision(transition.From),
            ParseRevision(transition.To, "RevisionConfirmed To"),
            transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.Cancelled => new PendingRfqCancelledEvent(
            transition.CaseId, transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.Reopened => new PendingRfqReopenedEvent(
            transition.CaseId, transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.PickedUp => new PendingRfqPickedUpEvent(
            transition.CaseId, ParseUser(transition.To, "PickedUp To"),
            transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.Released => new PendingRfqReleasedEvent(
            transition.CaseId, ParseUser(transition.To, "Released To"),
            transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.AssignedTraderChanged => new PendingRfqAssignedTraderChangedEvent(
            transition.CaseId, ParseUser(transition.From, "AssignedTraderChanged From"),
            ParseUser(transition.To, "AssignedTraderChanged To"),
            transition.PerformedBy, transition.OccurredAt),
        RfqTransitionKind.TakenOver => new PendingRfqTakenOverEvent(
            transition.CaseId, ParseUser(transition.From, "TakenOver From"),
            ParseUser(transition.To, "TakenOver To"),
            transition.PerformedBy, transition.OccurredAt),
        _ => throw new DomainInvariantException(
            $"Unsupported RFQ transition kind '{transition.Kind}'."),
    });

    internal void Clear() => pending.Clear();

    private static QuoteId Required(QuoteId? value, string field) => value
        ?? throw new DomainInvariantException($"{field} is required.");

    private static UserId ParseUser(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainInvariantException($"{field} is required.");
        try { return UserId.Create(value); }
        catch (DomainValidationException exception)
        { throw new DomainInvariantException($"{field} is invalid: {exception.Message}"); }
    }

    private static RevisionId ParseRevision(string? value, string field)
    {
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
            throw new DomainInvariantException($"{field} is invalid.");
        return new RevisionId(parsed);
    }

    private static RevisionId? ParseOptionalRevision(string? value) =>
        value is null ? null : ParseRevision(value, "RevisionConfirmed From");

    private static RfqStatus ParseStatus(string? value, string field)
    {
        if (!Enum.TryParse<RfqStatus>(value, ignoreCase: false, out var status)
            || !Enum.IsDefined(status))
            throw new DomainInvariantException($"{field} is invalid.");
        return status;
    }
}

internal abstract record PendingEvent(UserId ActorUserId, DateTimeOffset OccurredAt);
internal abstract record PendingRfqEvent(CaseId CaseId, UserId ActorUserId, DateTimeOffset OccurredAt)
    : PendingEvent(ActorUserId, OccurredAt);
internal abstract record PendingQuoteEvent(QuoteId QuoteId, UserId ActorUserId, DateTimeOffset OccurredAt)
    : PendingEvent(ActorUserId, OccurredAt);

internal sealed record PendingRfqClosedHitEvent(CaseId CaseId, QuoteId QuoteId, UserId ActorUserId,
    DateTimeOffset OccurredAt) : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqClosedAwayEvent(CaseId CaseId, QuoteId QuoteId, UserId ActorUserId,
    DateTimeOffset OccurredAt) : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqOutcomeCorrectedEvent(CaseId CaseId, QuoteId QuoteId,
    RfqStatus From, RfqStatus To, string? Reason, UserId ActorUserId, DateTimeOffset OccurredAt)
    : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqContactOwnerChangedEvent(CaseId CaseId, UserId From, UserId To,
    UserId ActorUserId, DateTimeOffset OccurredAt) : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqRevisionConfirmedEvent(CaseId CaseId, RevisionId? From, RevisionId To,
    UserId ActorUserId, DateTimeOffset OccurredAt) : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqCancelledEvent(CaseId CaseId, UserId ActorUserId, DateTimeOffset OccurredAt)
    : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqReopenedEvent(CaseId CaseId, UserId ActorUserId, DateTimeOffset OccurredAt)
    : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqPickedUpEvent(CaseId CaseId, UserId AssignedTraderId,
    UserId ActorUserId, DateTimeOffset OccurredAt) : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqReleasedEvent(CaseId CaseId, UserId AssignedTraderId,
    UserId ActorUserId, DateTimeOffset OccurredAt) : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqAssignedTraderChangedEvent(CaseId CaseId, UserId From, UserId To,
    UserId ActorUserId, DateTimeOffset OccurredAt) : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);
internal sealed record PendingRfqTakenOverEvent(CaseId CaseId, UserId From, UserId To,
    UserId ActorUserId, DateTimeOffset OccurredAt) : PendingRfqEvent(CaseId, ActorUserId, OccurredAt);

internal sealed record PendingQuoteConfirmedEvent(QuoteId QuoteId, UserId ActorUserId,
    DateTimeOffset OccurredAt) : PendingQuoteEvent(QuoteId, ActorUserId, OccurredAt);
internal sealed record PendingQuotePresentedEvent(QuoteId QuoteId, UserId ActorUserId,
    DateTimeOffset OccurredAt) : PendingQuoteEvent(QuoteId, ActorUserId, OccurredAt);
internal sealed record PendingQuoteUnpresentedEvent(QuoteId QuoteId, UserId ActorUserId,
    DateTimeOffset OccurredAt) : PendingQuoteEvent(QuoteId, ActorUserId, OccurredAt);
internal sealed record PendingQuoteWithdrawnEvent(QuoteId QuoteId, UserId ActorUserId,
    DateTimeOffset OccurredAt) : PendingQuoteEvent(QuoteId, ActorUserId, OccurredAt);
internal sealed record PendingQuoteExpiredEvent(QuoteId QuoteId, UserId ActorUserId,
    DateTimeOffset OccurredAt) : PendingQuoteEvent(QuoteId, ActorUserId, OccurredAt);
