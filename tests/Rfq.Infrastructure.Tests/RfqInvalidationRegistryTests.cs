using System.Collections.Immutable;
using Rfq.Application;
using Rfq.Domain;
using Rfq.Infrastructure;
using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class RfqInvalidationRegistryTests
{
    private static readonly DateOnly Today = new(2026, 9, 23);
    private static readonly CaseId CaseId = new(101);

    [Fact]
    public async Task Route_change_wakes_old_and_new_audiences_but_not_unrelated_subscriber()
    {
        var registry = new RfqInvalidationRegistry();
        await using RfqInvalidationSubscription oldAudience = registry.Subscribe(
            Identity("sales-old", "desk-old", UserRole.Sales));
        await using RfqInvalidationSubscription newAudience = registry.Subscribe(
            Identity("sales-new", "desk-new", UserRole.Sales));
        await using RfqInvalidationSubscription unrelated = registry.Subscribe(
            Identity("other", "desk-other", UserRole.Sales));
        await using RfqInvalidationSubscription oldDesk = registry.Subscribe(
            Identity("trader-old", "desk-old", UserRole.Trader));
        await using RfqInvalidationSubscription newDesk = registry.Subscribe(
            Identity("trader-new", "desk-new", UserRole.Trader));
        await oldAudience.WaitAsync(CancellationToken.None);
        await newAudience.WaitAsync(CancellationToken.None);
        await unrelated.WaitAsync(CancellationToken.None);
        await oldDesk.WaitAsync(CancellationToken.None);
        await newDesk.WaitAsync(CancellationToken.None);

        registry.Publish(
            Snapshot(Route("sales-old", "trader-old", "desk-old"), 1),
            Snapshot(Route("sales-new", "trader-new", "desk-new"), 2));

        Assert.True((await oldAudience.WaitAsync(CancellationToken.None))
            .HasFlag(RfqInvalidationCategory.SalesList));
        Assert.True((await newAudience.WaitAsync(CancellationToken.None))
            .HasFlag(RfqInvalidationCategory.SalesList));
        Assert.True((await oldDesk.WaitAsync(CancellationToken.None))
            .HasFlag(RfqInvalidationCategory.TraderList));
        Assert.True((await newDesk.WaitAsync(CancellationToken.None))
            .HasFlag(RfqInvalidationCategory.TraderList));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => unrelated.WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Same_user_tabs_are_both_woken_and_slow_signals_coalesce()
    {
        var registry = new RfqInvalidationRegistry();
        await using RfqInvalidationSubscription first = registry.Subscribe(
            Identity("sales-old", "desk-old", UserRole.Sales));
        await using RfqInvalidationSubscription second = registry.Subscribe(
            Identity("sales-old", "desk-old", UserRole.Sales));
        await first.WaitAsync(CancellationToken.None);
        await second.WaitAsync(CancellationToken.None);
        BusinessDateRfqSnapshot previous = Snapshot(
            Route("sales-old", "trader-old", "desk-old"), 1);
        BusinessDateRfqSnapshot current = Snapshot(
            Route("sales-new", "trader-new", "desk-new"), 2);

        registry.Publish(previous, current);
        registry.Publish(previous, current);

        Assert.NotEqual(RfqInvalidationCategory.None, await first.WaitAsync(CancellationToken.None));
        Assert.NotEqual(RfqInvalidationCategory.None, await second.WaitAsync(CancellationToken.None));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => first.WaitAsync(timeout.Token));
    }

    private static RfqSubscriberIdentity Identity(
        string userId,
        string deskId,
        UserRole role) => new(
            UserId.Create(userId),
            DeskId.Create(deskId),
            new HashSet<UserRole> { role });

    private static RfqSnapshotRoute Route(
        string contactOwner,
        string trader,
        string desk) => new(
            CaseId,
            UserId.Create(contactOwner),
            UserId.Create(contactOwner),
            UserId.Create(trader),
            DeskId.Create(desk),
            new RevisionId(Guid.Parse("00000000-0000-0000-0000-000000000101")),
            null);

    private static BusinessDateRfqSnapshot Snapshot(
        RfqSnapshotRoute route,
        long generation) => new(
            Today,
            generation,
            [],
            [],
            ImmutableDictionary<CaseId, RfqSnapshotRoute>.Empty.Add(CaseId, route));
}
