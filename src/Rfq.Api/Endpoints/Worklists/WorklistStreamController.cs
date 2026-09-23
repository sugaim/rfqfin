using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Infrastructure;

namespace Rfq.Api.Endpoints.Worklists;

[ApiController]
[Route("api/worklists/stream")]
public sealed class WorklistStreamController(
    RfqInvalidationRegistry invalidations,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [EndpointName("StreamWorklistInvalidations")]
    [Produces("text/event-stream")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task Stream(CancellationToken cancellationToken = default)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        global::Rfq.Application.CurrentUser user = currentUser.User;
        await using RfqInvalidationSubscription subscription = invalidations.Subscribe(
            new RfqSubscriberIdentity(user.UserId, user.DeskId, user.Roles));
        await Response.WriteAsync(": connected\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RfqInvalidationCategory categories = await subscription.WaitAsync(cancellationToken);
                string payload = string.Join(',', GetCategoryNames(categories));
                await Response.WriteAsync(
                    $"event: invalidation\ndata: {payload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private static IEnumerable<string> GetCategoryNames(RfqInvalidationCategory categories)
    {
        if (categories.HasFlag(RfqInvalidationCategory.SalesList))
        {
            yield return "sales-list";
        }

        if (categories.HasFlag(RfqInvalidationCategory.TraderList))
        {
            yield return "trader-list";
        }

        if (categories.HasFlag(RfqInvalidationCategory.RecentRevisions))
        {
            yield return "recent-revisions";
        }

        if (categories.HasFlag(RfqInvalidationCategory.BusinessDate))
        {
            yield return "business-date";
        }
    }
}
