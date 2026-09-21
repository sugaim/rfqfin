using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;
using System.Text.Json;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController(IEventFeed events) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PersistedEventResponse>>> GetAfter(
        [FromQuery] long after = 0,
        CancellationToken cancellationToken = default)
    {
        var items = await events.GetAfterAsync(after, cancellationToken);
        return Ok(items.Select(PersistedEventResponse.From).ToArray());
    }

    [HttpGet("stream")]
    public async Task Stream([FromQuery] long after = 0,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        var cursor = after;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        await Response.WriteAsync(": connected\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var latest = await events.GetLatestIdAsync(cancellationToken);
            if (latest <= cursor) continue;
            cursor = latest;
            await Response.WriteAsync($"event: changed\ndata: {latest}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}

public sealed record PersistedEventResponse(
    long EventId,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    long CaseId,
    string Kind,
    string Type,
    string PayloadJson)
{
    public static PersistedEventResponse From(EventFeedItem item)
    {
        var description = Describe(item);
        return new(
        item.EventId,
        item.OccurredAt,
        item.ActorUserId?.Value,
        description.CaseId.Value,
        description.Kind,
        description.Type,
        description.PayloadJson);
    }

    private static (CaseId CaseId, string Kind, string Type, string PayloadJson) Describe(
        EventFeedItem item) => item switch
        {
            RfqEvent rfq => DescribeRfq(rfq),
            QuoteEvent quote => DescribeQuote(quote),
            _ => throw new InvalidOperationException(
                $"Unsupported event type '{item.GetType().Name}'."),
        };

    private static (CaseId, string, string, string) DescribeRfq(RfqEvent item)
    {
        var (kind, quoteId, from, to, reason) = item switch
        {
            RfqClosedHitEvent value => (RfqTransitionKind.ClosedHit, (Guid?)value.QuoteId.Value, null, null, null),
            RfqClosedAwayEvent value => (RfqTransitionKind.ClosedAway, (Guid?)value.QuoteId.Value, null, null, null),
            RfqOutcomeCorrectedEvent value => (RfqTransitionKind.OutcomeCorrected,
                (Guid?)value.QuoteId.Value, value.From.ToString(), value.To.ToString(), value.Reason),
            RfqContactOwnerChangedEvent value => (RfqTransitionKind.ContactOwnerChanged,
                null, value.From.Value, value.To.Value, null),
            RfqRevisionConfirmedEvent value => (RfqTransitionKind.RevisionConfirmed,
                null, value.From?.Value.ToString(), value.To.Value.ToString(), null),
            RfqCancelledEvent => (RfqTransitionKind.Cancelled, null, null, null, null),
            RfqReopenedEvent => (RfqTransitionKind.Reopened, null, null, null, null),
            RfqPickedUpEvent value => (RfqTransitionKind.PickedUp, null, null, value.AssignedTraderId.Value, null),
            RfqReleasedEvent value => (RfqTransitionKind.Released, null, null, value.AssignedTraderId.Value, null),
            RfqAssignedTraderChangedEvent value => (RfqTransitionKind.AssignedTraderChanged,
                null, value.From.Value, value.To.Value, null),
            RfqTakenOverEvent value => (RfqTransitionKind.TakenOver,
                null, value.From.Value, value.To.Value, null),
            _ => throw new InvalidOperationException(
                $"Unsupported RFQ event type '{item.GetType().Name}'."),
        };
        var payload = JsonSerializer.Serialize(new
        {
            Kind = kind,
            CaseId = item.CaseId.Value,
            PerformedBy = item.ActorUserId?.Value,
            item.OccurredAt,
            QuoteId = quoteId,
            From = from,
            To = to,
            Reason = reason,
        });
        return (item.CaseId, "Rfq", kind.ToString(), payload);
    }

    private static (CaseId, string, string, string) DescribeQuote(QuoteEvent item)
    {
        var kind = item switch
        {
            QuoteConfirmedEvent => QuoteTransitionKind.Confirmed,
            QuotePresentedEvent => QuoteTransitionKind.Presented,
            QuoteUnpresentedEvent => QuoteTransitionKind.Unpresented,
            QuoteWithdrawnEvent => QuoteTransitionKind.Withdrawn,
            QuoteExpiredEvent => QuoteTransitionKind.Expired,
            _ => throw new InvalidOperationException(
                $"Unsupported Quote event type '{item.GetType().Name}'."),
        };
        var payload = JsonSerializer.Serialize(new
        {
            Kind = kind,
            QuoteId = item.QuoteId.Value,
            PerformedBy = item.ActorUserId?.Value,
            item.OccurredAt,
        });
        return (item.CaseId, "Quote", kind.ToString(), payload);
    }
}
