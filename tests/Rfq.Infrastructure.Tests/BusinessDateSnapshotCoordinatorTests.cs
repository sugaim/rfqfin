using System.Collections.Concurrent;
using System.Collections.Immutable;
using Rfq.Application;
using Rfq.Infrastructure;
using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class BusinessDateSnapshotCoordinatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 23);

    [Fact]
    public async Task Invalidations_during_load_converge_to_latest_generation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generations = new ConcurrentQueue<long>();
        int loads = 0;
        using BusinessDateRfqSnapshotCoordinator coordinator = Create(async (_, generation, _) =>
        {
            generations.Enqueue(generation);
            if (Interlocked.Increment(ref loads) == 1)
            {
                started.SetResult();
                await release.Task;
            }
            return EmptySnapshot(generation);
        });

        Task<BusinessDateRfqSnapshot> read = coordinator.GetSnapshotAsync(Today);
        await started.Task;
        for (int index = 0; index < 10; index++)
        {
            coordinator.SignalCommittedChange();
        }
        release.SetResult();

        BusinessDateRfqSnapshot snapshot = await read;
        Assert.Equal(2, loads);
        Assert.Equal(generations.Last(), snapshot.Generation);
        Assert.Equal(coordinator.GetStatus().CurrentGeneration, snapshot.Generation);
    }

    [Fact]
    public async Task Concurrent_dirty_reads_share_one_refresh()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int loads = 0;
        using BusinessDateRfqSnapshotCoordinator coordinator = Create(async (_, generation, _) =>
        {
            Interlocked.Increment(ref loads);
            started.SetResult();
            await release.Task;
            return EmptySnapshot(generation);
        });

        Task<BusinessDateRfqSnapshot> first = coordinator.GetSnapshotAsync(Today);
        await started.Task;
        Task<BusinessDateRfqSnapshot> second = coordinator.GetSnapshotAsync(Today);
        Assert.Equal(1, loads);
        release.SetResult();

        BusinessDateRfqSnapshot[] snapshots = await Task.WhenAll(first, second);
        Assert.Same(snapshots[0], snapshots[1]);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Cancelling_one_waiter_does_not_cancel_the_shared_refresh()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using BusinessDateRfqSnapshotCoordinator coordinator = Create(async (_, generation, cancellationToken) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(cancellationToken);
            return EmptySnapshot(generation);
        });
        using var cancelledWaiter = new CancellationTokenSource();

        Task<BusinessDateRfqSnapshot> first = coordinator.GetSnapshotAsync(
            Today, cancelledWaiter.Token);
        await started.Task;
        Task<BusinessDateRfqSnapshot> second = coordinator.GetSnapshotAsync(Today);
        cancelledWaiter.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.SetResult();
        BusinessDateRfqSnapshot snapshot = await second;

        Assert.Equal(Today, snapshot.BusinessDate);
    }

    [Fact]
    public async Task Disposing_coordinator_cancels_the_underlying_refresh()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        BusinessDateRfqSnapshotCoordinator coordinator = Create(async (_, generation, cancellationToken) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return EmptySnapshot(generation);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled.SetResult();
                throw;
            }
        });

        Task<BusinessDateRfqSnapshot> refresh = coordinator.GetSnapshotAsync(Today);
        await started.Task;
        coordinator.Dispose();

        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<RfqReadModelUnavailableException>(() => refresh);
    }

    [Fact]
    public async Task Retry_exhaustion_does_not_serve_a_known_stale_snapshot()
    {
        bool fail = false;
        using BusinessDateRfqSnapshotCoordinator coordinator = Create(
            (_, generation, _) =>
            {
                if (fail)
                {
                    throw new InvalidOperationException("database unavailable");
                }
                return Task.FromResult(EmptySnapshot(generation));
            },
            retryCount: 2);
        BusinessDateRfqSnapshot initial = await coordinator.GetSnapshotAsync(Today);
        fail = true;
        coordinator.SignalCommittedChange();

        await Assert.ThrowsAsync<RfqReadModelUnavailableException>(
            () => coordinator.GetSnapshotAsync(Today));
        RfqReadModelStatus status = coordinator.GetStatus();
        Assert.False(status.Available);
        Assert.Equal(initial.Generation, status.PublishedGeneration);
        Assert.True(status.CurrentGeneration > status.PublishedGeneration);
    }

    private static BusinessDateRfqSnapshotCoordinator Create(
        Func<DateOnly, long, CancellationToken, Task<BusinessDateRfqSnapshot>> load,
        int retryCount = 1) => new(
            _ => Task.FromResult(Today),
            load,
            new RfqRuntimeOptions
            {
                SnapshotRefreshRetryCount = retryCount,
                SnapshotRefreshRetryDelay = TimeSpan.FromMilliseconds(1),
            },
            new RfqInvalidationRegistry(),
            TimeProvider.System);

    private static BusinessDateRfqSnapshot EmptySnapshot(long generation) => new(
        Today,
        generation,
        [],
        [],
        ImmutableDictionary<Rfq.Domain.CaseId, RfqSnapshotRoute>.Empty);
}
