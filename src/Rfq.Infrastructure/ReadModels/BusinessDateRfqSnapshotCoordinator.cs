using Microsoft.Extensions.DependencyInjection;
using Rfq.Application;

namespace Rfq.Infrastructure;

public interface ICommittedRfqChangeSignal
{
    void SignalCommittedChange();
}

public sealed class BusinessDateRfqSnapshotCoordinator : ICommittedRfqChangeSignal, IDisposable
{
    private readonly Func<CancellationToken, Task<DateOnly>> getBusinessDate;
    private readonly Func<DateOnly, long, CancellationToken, Task<BusinessDateRfqSnapshot>> load;
    private readonly RfqRuntimeOptions options;
    private readonly RfqInvalidationRegistry invalidations;
    private readonly TimeProvider timeProvider;
    private readonly object gate = new();
    private readonly SemaphoreSlim refreshRequested = new(0, 1);
    private BusinessDateRfqSnapshot? snapshot;
    private Task<BusinessDateRfqSnapshot>? refreshTask;
    private DateOnly? requiredBusinessDate;
    private long currentGeneration = 1;
    private DateTimeOffset? lastSuccessfulRefreshAt;
    private Exception? lastFailure;

    public BusinessDateRfqSnapshotCoordinator(
        IServiceScopeFactory scopeFactory,
        RfqRuntimeOptions options,
        RfqInvalidationRegistry invalidations,
        TimeProvider timeProvider)
        : this(
            cancellationToken => GetAuthoritativeBusinessDateAsync(
                scopeFactory, cancellationToken),
            (businessDate, generation, cancellationToken) => LoadAsync(
                scopeFactory, businessDate, generation, cancellationToken),
            options,
            invalidations,
            timeProvider)
    { }

    internal BusinessDateRfqSnapshotCoordinator(
        Func<CancellationToken, Task<DateOnly>> getBusinessDate,
        Func<DateOnly, long, CancellationToken, Task<BusinessDateRfqSnapshot>> load,
        RfqRuntimeOptions options,
        RfqInvalidationRegistry invalidations,
        TimeProvider timeProvider)
    {
        this.getBusinessDate = getBusinessDate;
        this.load = load;
        this.options = options;
        this.invalidations = invalidations;
        this.timeProvider = timeProvider;
    }

    // Single API process assumption: invalidation and SSE routing are process-local. If the
    // service is scaled out, add DB-backed cross-process notification (for example PostgreSQL
    // LISTEN/NOTIFY) so every node refreshes and wakes its local subscribers; sticky sessions
    // alone are not a consistency mechanism.
    public void SignalCommittedChange()
    {
        Interlocked.Increment(ref currentGeneration);
        if (refreshRequested.CurrentCount == 0)
        {
            refreshRequested.Release();
        }
    }

    public async Task<BusinessDateRfqSnapshot> GetSnapshotAsync(
        DateOnly requestedBusinessDate,
        CancellationToken cancellationToken = default)
    {
        BusinessDateRfqSnapshot? current = Volatile.Read(ref snapshot);
        long generation = Interlocked.Read(ref currentGeneration);
        if (current?.BusinessDate == requestedBusinessDate
            && current.Generation == generation)
        {
            return current;
        }

        if (current is null || current.BusinessDate != requestedBusinessDate)
        {
            DateOnly authoritative = await getBusinessDate(cancellationToken);
            if (authoritative != requestedBusinessDate)
            {
                await EnsureSnapshotAsync(authoritative, cancellationToken);
                throw new BusinessDateChangedException(authoritative);
            }
        }

        return await EnsureSnapshotAsync(requestedBusinessDate, cancellationToken);
    }

    public async Task<BusinessDateRfqSnapshot> RefreshAuthoritativeAsync(
        CancellationToken cancellationToken = default)
    {
        DateOnly businessDate = await getBusinessDate(cancellationToken);
        return await EnsureSnapshotAsync(businessDate, cancellationToken);
    }

    public async Task WaitForRefreshRequestAsync(CancellationToken cancellationToken) =>
        await refreshRequested.WaitAsync(cancellationToken);

    public RfqReadModelStatus GetStatus()
    {
        BusinessDateRfqSnapshot? current = Volatile.Read(ref snapshot);
        long generation = Interlocked.Read(ref currentGeneration);
        lock (gate)
        {
            bool available = current is not null
                && current.BusinessDate == requiredBusinessDate
                && current.Generation == generation
                && lastFailure is null;
            return new RfqReadModelStatus(
                available,
                requiredBusinessDate,
                current?.BusinessDate,
                generation,
                current?.Generation ?? 0,
                lastSuccessfulRefreshAt,
                lastFailure?.GetType().Name);
        }
    }

    private async Task<BusinessDateRfqSnapshot> EnsureSnapshotAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        Task<BusinessDateRfqSnapshot> task;
        lock (gate)
        {
            if (requiredBusinessDate != businessDate)
            {
                requiredBusinessDate = businessDate;
                Interlocked.Increment(ref currentGeneration);
            }

            BusinessDateRfqSnapshot? current = snapshot;
            long generation = Interlocked.Read(ref currentGeneration);
            if (current?.BusinessDate == businessDate && current.Generation == generation)
            {
                return current;
            }

            if (refreshTask is null || refreshTask.IsCompleted)
            {
                refreshTask = RefreshUntilCurrentAsync();
            }
            task = refreshTask;
        }

        try
        {
            return await task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RfqReadModelUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new RfqReadModelUnavailableException(
                "The current RFQ worklist is temporarily unavailable.", exception);
        }
    }

    private async Task<BusinessDateRfqSnapshot> RefreshUntilCurrentAsync()
    {
        while (true)
        {
            DateOnly businessDate;
            long targetGeneration;
            lock (gate)
            {
                businessDate = requiredBusinessDate
                    ?? throw new InvalidOperationException("The snapshot Business Date is not set.");
                targetGeneration = Interlocked.Read(ref currentGeneration);
            }

            BusinessDateRfqSnapshot loaded;
            try
            {
                loaded = await LoadWithRetryAsync(businessDate, targetGeneration);
            }
            catch (Exception exception)
            {
                lock (gate)
                {
                    lastFailure = exception;
                }
                throw new RfqReadModelUnavailableException(
                    "The current RFQ worklist is temporarily unavailable.", exception);
            }

            lock (gate)
            {
                if (requiredBusinessDate != businessDate
                    || Interlocked.Read(ref currentGeneration) != targetGeneration)
                {
                    continue;
                }

                BusinessDateRfqSnapshot? previous = snapshot;
                Volatile.Write(ref snapshot, loaded);
                lastFailure = null;
                lastSuccessfulRefreshAt = timeProvider.GetUtcNow();
                invalidations.Publish(previous, loaded);
                return loaded;
            }
        }
    }

    private async Task<BusinessDateRfqSnapshot> LoadWithRetryAsync(
        DateOnly businessDate,
        long generation)
    {
        Exception? last = null;
        int attempts = Math.Max(1, options.SnapshotRefreshRetryCount);
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return await load(businessDate, generation, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                last = exception;
                if (attempt < attempts)
                {
                    await Task.Delay(options.SnapshotRefreshRetryDelay);
                }
            }
        }

        throw last ?? new InvalidOperationException("Snapshot loading failed.");
    }

    private static async Task<DateOnly> GetAuthoritativeBusinessDateAsync(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IBusinessDateProvider provider = scope.ServiceProvider
            .GetRequiredService<IBusinessDateProvider>();
        return await provider.GetCurrentAsync(cancellationToken);
    }

    private static async Task<BusinessDateRfqSnapshot> LoadAsync(
        IServiceScopeFactory scopeFactory,
        DateOnly businessDate,
        long generation,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        BusinessDateRfqSnapshotLoader loader = scope.ServiceProvider
            .GetRequiredService<BusinessDateRfqSnapshotLoader>();
        return await loader.LoadAsync(businessDate, generation, cancellationToken);
    }

    public void Dispose() => refreshRequested.Dispose();
}
