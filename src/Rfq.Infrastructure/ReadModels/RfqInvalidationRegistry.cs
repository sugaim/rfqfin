using System.Collections.Concurrent;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

[Flags]
public enum RfqInvalidationCategory
{
    None = 0,
    SalesList = 1,
    TraderList = 2,
    RecentRevisions = 4,
    BusinessDate = 8,
}

public sealed record RfqSubscriberIdentity(
    UserId UserId,
    DeskId DeskId,
    IReadOnlySet<UserRole> Roles);

public sealed class RfqInvalidationSubscription : IAsyncDisposable
{
    private readonly RfqInvalidationRegistry owner;
    private readonly Guid id;
    private readonly SemaphoreSlim signal = new(0, 1);
    private int pending;
    private int wakeScheduled;

    internal RfqInvalidationSubscription(
        RfqInvalidationRegistry owner,
        Guid id,
        RfqSubscriberIdentity identity)
    {
        this.owner = owner;
        this.id = id;
        Identity = identity;
    }

    public RfqSubscriberIdentity Identity { get; }

    internal void Publish(RfqInvalidationCategory categories)
    {
        if (categories == RfqInvalidationCategory.None)
        {
            return;
        }

        Interlocked.Or(ref pending, (int)categories);
        if (Interlocked.Exchange(ref wakeScheduled, 1) == 0)
        {
            signal.Release();
        }
    }

    public async Task<RfqInvalidationCategory> WaitAsync(CancellationToken cancellationToken)
    {
        await signal.WaitAsync(cancellationToken);
        RfqInvalidationCategory categories =
            (RfqInvalidationCategory)Interlocked.Exchange(ref pending, 0);
        Interlocked.Exchange(ref wakeScheduled, 0);
        if (Volatile.Read(ref pending) != 0
            && Interlocked.Exchange(ref wakeScheduled, 1) == 0)
        {
            signal.Release();
        }
        return categories;
    }

    public ValueTask DisposeAsync()
    {
        owner.Remove(id);
        return ValueTask.CompletedTask;
    }
}

public sealed class RfqInvalidationRegistry
{
    private readonly ConcurrentDictionary<Guid, RfqInvalidationSubscription> subscriptions = [];

    public RfqInvalidationSubscription Subscribe(RfqSubscriberIdentity identity)
    {
        var id = Guid.NewGuid();
        var subscription = new RfqInvalidationSubscription(this, id, identity);
        subscriptions[id] = subscription;
        RfqInvalidationCategory initial = RfqInvalidationCategory.BusinessDate;
        if (identity.Roles.Contains(UserRole.Sales))
        {
            initial |= RfqInvalidationCategory.SalesList
                | RfqInvalidationCategory.RecentRevisions;
        }
        if (identity.Roles.Contains(UserRole.Trader))
        {
            initial |= RfqInvalidationCategory.TraderList;
        }
        subscription.Publish(initial);
        return subscription;
    }

    internal void Publish(
        BusinessDateRfqSnapshot? previous,
        BusinessDateRfqSnapshot current)
    {
        if (previous is null || previous.BusinessDate != current.BusinessDate)
        {
            PublishAll(RfqInvalidationCategory.BusinessDate
                | RfqInvalidationCategory.SalesList
                | RfqInvalidationCategory.TraderList
                | RfqInvalidationCategory.RecentRevisions);
            return;
        }

        var oldSales = previous.Sales.ToDictionary(item => item.CaseId);
        var newSales = current.Sales.ToDictionary(item => item.CaseId);
        var oldTrader = previous.Trader.ToDictionary(item => item.CaseId);
        var newTrader = current.Trader.ToDictionary(item => item.CaseId);
        foreach (CaseId caseId in previous.Routes.Keys.Union(current.Routes.Keys))
        {
            previous.Routes.TryGetValue(caseId, out RfqSnapshotRoute? oldRoute);
            current.Routes.TryGetValue(caseId, out RfqSnapshotRoute? newRoute);
            bool routeChanged = oldRoute != newRoute;
            bool salesChanged = routeChanged || !SameValue(oldSales, newSales, caseId);
            bool traderChanged = routeChanged || !SameValue(oldTrader, newTrader, caseId);
            bool recentChanged = oldRoute?.CurrentRevisionId != newRoute?.CurrentRevisionId
                || oldRoute?.CurrentQuoteId != newRoute?.CurrentQuoteId;
            if (!salesChanged && !traderChanged && !recentChanged)
            {
                continue;
            }

            foreach (RfqInvalidationSubscription subscription in subscriptions.Values)
            {
                RfqInvalidationCategory categories = RfqInvalidationCategory.None;
                if (salesChanged
                    && subscription.Identity.Roles.Contains(UserRole.Sales)
                    && IsSalesAudience(subscription.Identity.UserId, oldRoute, newRoute))
                {
                    categories |= RfqInvalidationCategory.SalesList;
                }
                if (traderChanged
                    && subscription.Identity.Roles.Contains(UserRole.Trader)
                    && IsTraderAudience(subscription.Identity.DeskId, oldRoute, newRoute))
                {
                    categories |= RfqInvalidationCategory.TraderList;
                }
                if (recentChanged
                    && subscription.Identity.Roles.Contains(UserRole.Sales)
                    && IsSalesAudience(subscription.Identity.UserId, oldRoute, newRoute))
                {
                    categories |= RfqInvalidationCategory.RecentRevisions;
                }
                subscription.Publish(categories);
            }
        }
    }

    internal void Remove(Guid id) => subscriptions.TryRemove(id, out _);

    private void PublishAll(RfqInvalidationCategory categories)
    {
        foreach (RfqInvalidationSubscription subscription in subscriptions.Values)
        {
            subscription.Publish(categories);
        }
    }

    private static bool SameValue<T>(
        IReadOnlyDictionary<CaseId, T> previous,
        IReadOnlyDictionary<CaseId, T> current,
        CaseId caseId)
    {
        previous.TryGetValue(caseId, out T? oldValue);
        current.TryGetValue(caseId, out T? newValue);
        return EqualityComparer<T>.Default.Equals(oldValue, newValue);
    }

    private static bool IsSalesAudience(
        UserId userId,
        RfqSnapshotRoute? previous,
        RfqSnapshotRoute? current) =>
        previous?.SalesId == userId
        || previous?.ContactOwnerId == userId
        || current?.SalesId == userId
        || current?.ContactOwnerId == userId;

    private static bool IsTraderAudience(
        DeskId deskId,
        RfqSnapshotRoute? previous,
        RfqSnapshotRoute? current) =>
        previous?.AssignedTraderDeskId == deskId
        || current?.AssignedTraderDeskId == deskId;
}
