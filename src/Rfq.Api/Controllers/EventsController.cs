using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController(IEventFeed events) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PersistedEvent>>> GetAfter(
        [FromQuery] long after = 0,
        CancellationToken cancellationToken = default) =>
        Ok(await events.GetAfterAsync(after, cancellationToken));

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
