using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Events;

[ApiController]
[Route("api/events")]
public sealed class EventsController(IEventFeed events) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<EventResponse>> GetAfter(
        [FromQuery] long after = 0,
        CancellationToken cancellationToken = default) =>
        [.. (await events.GetAfterAsync(after, cancellationToken)).Select(EventsApiMapper.ToApi)];

    [HttpGet("stream")]
    public async Task Stream(
        [FromQuery] long after = 0,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        long cursor = after;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        await Response.WriteAsync(": connected\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            long latest = await events.GetLatestIdAsync(cancellationToken);
            if (latest <= cursor)
            {
                continue;
            }

            cursor = latest;
            await Response.WriteAsync($"event: changed\ndata: {latest}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RfqClosedHitResponse), "rfqClosedHit")]
[JsonDerivedType(typeof(RfqClosedAwayResponse), "rfqClosedAway")]
[JsonDerivedType(typeof(RfqOutcomeCorrectedResponse), "rfqOutcomeCorrected")]
[JsonDerivedType(typeof(RfqContactOwnerChangedResponse), "rfqContactOwnerChanged")]
[JsonDerivedType(typeof(RfqRevisionConfirmedResponse), "rfqRevisionConfirmed")]
[JsonDerivedType(typeof(RfqCancelledResponse), "rfqCancelled")]
[JsonDerivedType(typeof(RfqReopenedResponse), "rfqReopened")]
[JsonDerivedType(typeof(RfqPickedUpResponse), "rfqPickedUp")]
[JsonDerivedType(typeof(RfqReleasedResponse), "rfqReleased")]
[JsonDerivedType(typeof(RfqAssignedTraderChangedResponse), "rfqAssignedTraderChanged")]
[JsonDerivedType(typeof(RfqTakenOverResponse), "rfqTakenOver")]
[JsonDerivedType(typeof(QuoteConfirmedResponse), "quoteConfirmed")]
[JsonDerivedType(typeof(QuotePresentedResponse), "quotePresented")]
[JsonDerivedType(typeof(QuoteUnpresentedResponse), "quoteUnpresented")]
[JsonDerivedType(typeof(QuoteWithdrawnResponse), "quoteWithdrawn")]
[JsonDerivedType(typeof(QuoteExpiredResponse), "quoteExpired")]
public abstract record EventResponse(long EventId, DateTimeOffset OccurredAt, string? ActorUserId);

public abstract record RfqEventResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId) : EventResponse(EventId, OccurredAt, ActorUserId);

public abstract record QuoteEventResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid QuoteId) : EventResponse(EventId, OccurredAt, ActorUserId);

public sealed record RfqClosedHitResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid QuoteId) : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqClosedAwayResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid QuoteId) : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqOutcomeCorrectedResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid QuoteId,
    string From,
    string To,
    string? Reason)
    : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqContactOwnerChangedResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    string From,
    string To)
    : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqRevisionConfirmedResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid? From,
    Guid To)
    : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqCancelledResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId) : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqReopenedResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId) : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqPickedUpResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    string AssignedTraderId)
    : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqReleasedResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    string AssignedTraderId)
    : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqAssignedTraderChangedResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    string From,
    string To)
    : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqTakenOverResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    string From,
    string To)
    : RfqEventResponse(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record QuoteConfirmedResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid QuoteId)
    : QuoteEventResponse(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

public sealed record QuotePresentedResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid QuoteId)
    : QuoteEventResponse(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

public sealed record QuoteUnpresentedResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid QuoteId)
    : QuoteEventResponse(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

public sealed record QuoteWithdrawnResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid QuoteId)
    : QuoteEventResponse(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

public sealed record QuoteExpiredResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    Guid QuoteId)
    : QuoteEventResponse(EventId, OccurredAt, ActorUserId, CaseId, QuoteId);

public static class EventsApiMapper
{
    public static EventResponse ToApi(EventFeedItem item) => item switch
    {
        RfqClosedHitEvent value => new RfqClosedHitResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.QuoteId.Value),
        RfqClosedAwayEvent value => new RfqClosedAwayResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.QuoteId.Value),
        RfqOutcomeCorrectedEvent value => new RfqOutcomeCorrectedResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.QuoteId.Value,
            value.From.ToString(),
            value.To.ToString(),
            value.Reason),
        RfqContactOwnerChangedEvent value => new RfqContactOwnerChangedResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.From.Value,
            value.To.Value),
        RfqRevisionConfirmedEvent value => new RfqRevisionConfirmedResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.From?.Value,
            value.To.Value),
        RfqCancelledEvent value => new RfqCancelledResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value),
        RfqReopenedEvent value => new RfqReopenedResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value),
        RfqPickedUpEvent value => new RfqPickedUpResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.AssignedTraderId.Value),
        RfqReleasedEvent value => new RfqReleasedResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.AssignedTraderId.Value),
        RfqAssignedTraderChangedEvent value => new RfqAssignedTraderChangedResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.From.Value,
            value.To.Value),
        RfqTakenOverEvent value => new RfqTakenOverResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.From.Value,
            value.To.Value),
        QuoteConfirmedEvent value => new QuoteConfirmedResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.QuoteId.Value),
        QuotePresentedEvent value => new QuotePresentedResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.QuoteId.Value),
        QuoteUnpresentedEvent value => new QuoteUnpresentedResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.QuoteId.Value),
        QuoteWithdrawnEvent value => new QuoteWithdrawnResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.QuoteId.Value),
        QuoteExpiredEvent value => new QuoteExpiredResponse(
            value.EventId,
            value.OccurredAt,
            value.ActorUserId?.Value,
            value.CaseId.Value,
            value.QuoteId.Value),
        _ => throw new InvalidOperationException($"Unsupported event '{item.GetType().Name}'."),
    };
}
