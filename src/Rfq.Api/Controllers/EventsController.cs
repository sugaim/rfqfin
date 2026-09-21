using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

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
    public static PersistedEventResponse From(PersistedEvent item) => new(
        item.EventId,
        item.OccurredAt,
        item.ActorUserId?.Value,
        item.CaseId.Value,
        item.Kind.ToString(),
        item.Type,
        item.PayloadJson);
}
