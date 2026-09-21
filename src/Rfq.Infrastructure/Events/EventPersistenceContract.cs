using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

internal static class EventPersistenceTypeCodes
{
    internal static class Rfq
    {
        internal const string ClosedHit = "ClosedHit";
        internal const string ClosedAway = "ClosedAway";
        internal const string OutcomeCorrected = "OutcomeCorrected";
        internal const string ContactOwnerChanged = "ContactOwnerChanged";
        internal const string RevisionConfirmed = "RevisionConfirmed";
        internal const string Cancelled = "Cancelled";
        internal const string Reopened = "Reopened";
        internal const string PickedUp = "PickedUp";
        internal const string Released = "Released";
        internal const string AssignedTraderChanged = "AssignedTraderChanged";
        internal const string TakenOver = "TakenOver";
    }

    internal static class Quote
    {
        internal const string Confirmed = "Confirmed";
        internal const string Presented = "Presented";
        internal const string Unpresented = "Unpresented";
        internal const string Withdrawn = "Withdrawn";
        internal const string Expired = "Expired";
    }
}

internal sealed record EventPersistenceData(string TypeCode, string PayloadJson);

internal static class EventPersistenceContract
{
    internal static EventPersistenceData Serialize(PendingEvent pending) => pending switch
    {
        PendingRfqClosedHitEvent item => Data(EventPersistenceTypeCodes.Rfq.ClosedHit,
            new ClosePayload { QuoteId = item.QuoteId.Value }),
        PendingRfqClosedAwayEvent item => Data(EventPersistenceTypeCodes.Rfq.ClosedAway,
            new ClosePayload { QuoteId = item.QuoteId.Value }),
        PendingRfqOutcomeCorrectedEvent item => Data(EventPersistenceTypeCodes.Rfq.OutcomeCorrected,
            new OutcomeCorrectedPayload
            {
                QuoteId = item.QuoteId.Value,
                From = OutcomeCode(item.From),
                To = OutcomeCode(item.To),
                Reason = item.Reason,
            }),
        PendingRfqContactOwnerChangedEvent item => Data(EventPersistenceTypeCodes.Rfq.ContactOwnerChanged,
            new UserChangePayload { From = item.From.Value, To = item.To.Value }),
        PendingRfqRevisionConfirmedEvent item => Data(EventPersistenceTypeCodes.Rfq.RevisionConfirmed,
            new RevisionConfirmedPayload { From = item.From?.Value, To = item.To.Value }),
        PendingRfqCancelledEvent => Empty(EventPersistenceTypeCodes.Rfq.Cancelled),
        PendingRfqReopenedEvent => Empty(EventPersistenceTypeCodes.Rfq.Reopened),
        PendingRfqPickedUpEvent item => Data(EventPersistenceTypeCodes.Rfq.PickedUp,
            new AssignedTraderPayload { AssignedTraderId = item.AssignedTraderId.Value }),
        PendingRfqReleasedEvent item => Data(EventPersistenceTypeCodes.Rfq.Released,
            new AssignedTraderPayload { AssignedTraderId = item.AssignedTraderId.Value }),
        PendingRfqAssignedTraderChangedEvent item => Data(
            EventPersistenceTypeCodes.Rfq.AssignedTraderChanged,
            new UserChangePayload { From = item.From.Value, To = item.To.Value }),
        PendingRfqTakenOverEvent item => Data(EventPersistenceTypeCodes.Rfq.TakenOver,
            new UserChangePayload { From = item.From.Value, To = item.To.Value }),
        PendingQuoteConfirmedEvent => Empty(EventPersistenceTypeCodes.Quote.Confirmed),
        PendingQuotePresentedEvent => Empty(EventPersistenceTypeCodes.Quote.Presented),
        PendingQuoteUnpresentedEvent => Empty(EventPersistenceTypeCodes.Quote.Unpresented),
        PendingQuoteWithdrawnEvent => Empty(EventPersistenceTypeCodes.Quote.Withdrawn),
        PendingQuoteExpiredEvent => Empty(EventPersistenceTypeCodes.Quote.Expired),
        _ => throw new DomainInvariantException(
            $"Unsupported pending event type '{pending.GetType().Name}'."),
    };

    internal static RfqEvent DeserializeRfq(long eventId, DateTimeOffset occurredAt,
        UserId? actorUserId, CaseId caseId, string typeCode, string payloadJson)
    {
        return typeCode switch
        {
            EventPersistenceTypeCodes.Rfq.ClosedHit => new RfqClosedHitEvent(
                eventId, occurredAt, actorUserId, caseId, QuoteIdOf(payloadJson)),
            EventPersistenceTypeCodes.Rfq.ClosedAway => new RfqClosedAwayEvent(
                eventId, occurredAt, actorUserId, caseId, QuoteIdOf(payloadJson)),
            EventPersistenceTypeCodes.Rfq.OutcomeCorrected => OutcomeCorrected(
                eventId, occurredAt, actorUserId, caseId, payloadJson),
            EventPersistenceTypeCodes.Rfq.ContactOwnerChanged => ContactOwnerChanged(
                eventId, occurredAt, actorUserId, caseId, payloadJson),
            EventPersistenceTypeCodes.Rfq.RevisionConfirmed => RevisionConfirmed(
                eventId, occurredAt, actorUserId, caseId, payloadJson),
            EventPersistenceTypeCodes.Rfq.Cancelled => EmptyRfq(payloadJson,
                () => new RfqCancelledEvent(eventId, occurredAt, actorUserId, caseId)),
            EventPersistenceTypeCodes.Rfq.Reopened => EmptyRfq(payloadJson,
                () => new RfqReopenedEvent(eventId, occurredAt, actorUserId, caseId)),
            EventPersistenceTypeCodes.Rfq.PickedUp => AssignedTrader(
                payloadJson, id => new RfqPickedUpEvent(
                    eventId, occurredAt, actorUserId, caseId, id)),
            EventPersistenceTypeCodes.Rfq.Released => AssignedTrader(
                payloadJson, id => new RfqReleasedEvent(
                    eventId, occurredAt, actorUserId, caseId, id)),
            EventPersistenceTypeCodes.Rfq.AssignedTraderChanged => UserChanged(
                payloadJson, (from, to) => new RfqAssignedTraderChangedEvent(
                    eventId, occurredAt, actorUserId, caseId, from, to)),
            EventPersistenceTypeCodes.Rfq.TakenOver => UserChanged(
                payloadJson, (from, to) => new RfqTakenOverEvent(
                    eventId, occurredAt, actorUserId, caseId, from, to)),
            _ => throw new DomainInvariantException(
                $"Persisted RFQ event type '{typeCode}' is invalid."),
        };
    }

    internal static QuoteEvent DeserializeQuote(long eventId, DateTimeOffset occurredAt,
        UserId? actorUserId, CaseId caseId, QuoteId quoteId, string typeCode, string payloadJson)
    {
        ValidateEmpty(payloadJson);
        return typeCode switch
        {
            EventPersistenceTypeCodes.Quote.Confirmed => new QuoteConfirmedEvent(
                eventId, occurredAt, actorUserId, caseId, quoteId),
            EventPersistenceTypeCodes.Quote.Presented => new QuotePresentedEvent(
                eventId, occurredAt, actorUserId, caseId, quoteId),
            EventPersistenceTypeCodes.Quote.Unpresented => new QuoteUnpresentedEvent(
                eventId, occurredAt, actorUserId, caseId, quoteId),
            EventPersistenceTypeCodes.Quote.Withdrawn => new QuoteWithdrawnEvent(
                eventId, occurredAt, actorUserId, caseId, quoteId),
            EventPersistenceTypeCodes.Quote.Expired => new QuoteExpiredEvent(
                eventId, occurredAt, actorUserId, caseId, quoteId),
            _ => throw new DomainInvariantException(
                $"Persisted Quote event type '{typeCode}' is invalid."),
        };
    }

    private static EventPersistenceData Data<T>(string code, T payload) where T : class =>
        new(code, PersistenceJsonSerializer.Serialize(payload));
    private static EventPersistenceData Empty(string code) => Data(code, new EmptyPayload());

    private static QuoteId QuoteIdOf(string json)
    {
        var value = PersistenceJsonSerializer.Deserialize<ClosePayload>(
            json, "RFQ close event payload").QuoteId;
        if (value == Guid.Empty)
            throw new DomainInvariantException("Persisted RFQ close event QuoteId is invalid.");
        return new QuoteId(value);
    }

    private static RfqOutcomeCorrectedEvent OutcomeCorrected(long id, DateTimeOffset at,
        UserId? actor, CaseId caseId, string json)
    {
        var dto = PersistenceJsonSerializer.Deserialize<OutcomeCorrectedPayload>(
            json, "outcome-corrected event payload");
        if (dto.QuoteId == Guid.Empty)
            throw new DomainInvariantException("Persisted outcome-corrected QuoteId is invalid.");
        return new RfqOutcomeCorrectedEvent(id, at, actor, caseId, new QuoteId(dto.QuoteId),
            ParseOutcome(dto.From), ParseOutcome(dto.To), dto.Reason);
    }

    private static RfqContactOwnerChangedEvent ContactOwnerChanged(long id, DateTimeOffset at,
        UserId? actor, CaseId caseId, string json) =>
        UserChanged(json, (from, to) => new RfqContactOwnerChangedEvent(
            id, at, actor, caseId, from, to));

    private static RfqRevisionConfirmedEvent RevisionConfirmed(long id, DateTimeOffset at,
        UserId? actor, CaseId caseId, string json)
    {
        var dto = PersistenceJsonSerializer.Deserialize<RevisionConfirmedPayload>(
            json, "revision-confirmed event payload");
        return new RfqRevisionConfirmedEvent(id, at, actor, caseId,
            dto.From is null ? null : ParseRevision(dto.From.Value), ParseRevision(dto.To));
    }

    private static T EmptyRfq<T>(string json, Func<T> factory) where T : RfqEvent
    {
        ValidateEmpty(json);
        return factory();
    }

    private static T AssignedTrader<T>(string json, Func<UserId, T> factory)
        where T : RfqEvent
    {
        var value = PersistenceJsonSerializer.Deserialize<AssignedTraderPayload>(
            json, "assigned-trader event payload").AssignedTraderId;
        return factory(ParseUser(value));
    }

    private static T UserChanged<T>(string json, Func<UserId, UserId, T> factory)
        where T : RfqEvent
    {
        var dto = PersistenceJsonSerializer.Deserialize<UserChangePayload>(
            json, "user-change event payload");
        return factory(ParseUser(dto.From), ParseUser(dto.To));
    }

    private static void ValidateEmpty(string json) =>
        _ = PersistenceJsonSerializer.Deserialize<EmptyPayload>(json, "empty event payload");

    private static UserId ParseUser(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainInvariantException("Persisted event UserId is missing.");
        try { return UserId.Create(value); }
        catch (DomainValidationException exception)
        { throw new DomainInvariantException($"Persisted event UserId is invalid: {exception.Message}"); }
    }

    private static RevisionId ParseRevision(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainInvariantException("Persisted event RevisionId is invalid.");
        return new RevisionId(value);
    }

    private static RfqStatus ParseOutcome(string? value) => value switch
    {
        "Hit" => RfqStatus.Hit,
        "Away" => RfqStatus.Away,
        _ => throw new DomainInvariantException(
            $"Persisted RFQ outcome '{value}' is invalid."),
    };

    private static string OutcomeCode(RfqStatus value) => value switch
    {
        RfqStatus.Hit => "Hit",
        RfqStatus.Away => "Away",
        _ => throw new DomainInvariantException(
            $"RFQ outcome '{value}' cannot be persisted as a correction."),
    };
}

internal sealed class EmptyPayload { }
internal sealed class ClosePayload { public required Guid QuoteId { get; init; } }
internal sealed class OutcomeCorrectedPayload
{
    public required Guid QuoteId { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required string? Reason { get; init; }
}
internal sealed class UserChangePayload
{
    public required string From { get; init; }
    public required string To { get; init; }
}
internal sealed class RevisionConfirmedPayload
{
    public required Guid? From { get; init; }
    public required Guid To { get; init; }
}
internal sealed class AssignedTraderPayload { public required string AssignedTraderId { get; init; } }
