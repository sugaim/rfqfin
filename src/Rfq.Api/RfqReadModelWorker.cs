using Rfq.Infrastructure;

namespace Rfq.Api;

public sealed class RfqReadModelWorker(
    BusinessDateRfqSnapshotCoordinator coordinator,
    RfqRuntimeOptions options,
    IIncidentReporter incidentReporter,
    ILogger<RfqReadModelWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogRefreshFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(LogRefreshFailure)),
            "RFQ read-model refresh failed during {Operation}.");

    private static readonly Action<ILogger, Exception?> LogIncidentFailure =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, nameof(LogIncidentFailure)),
            "RFQ read-model incident reporting failed.");

    private int incidentOpen;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshBoundaryAsync("InitialSnapshot", stoppingToken);
        await Task.WhenAll(
            RunInvalidationLoopAsync(stoppingToken),
            RunBusinessDatePollLoopAsync(stoppingToken),
            RunRecoveryLoopAsync(stoppingToken));
    }

    private async Task RunInvalidationLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                await coordinator.WaitForRefreshRequestAsync(stoppingToken);
                await Task.Delay(options.SnapshotRefreshCoalesce, stoppingToken);
                await RefreshBoundaryAsync("CommittedChange", stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task RunBusinessDatePollLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.BusinessDatePollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshBoundaryAsync("BusinessDatePoll", stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task RunRecoveryLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SnapshotRecoveryInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!coordinator.GetStatus().Available)
                {
                    await RefreshBoundaryAsync("UnavailableRecovery", stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    internal async Task RefreshBoundaryAsync(
        string operation,
        CancellationToken stoppingToken)
    {
        try
        {
            await coordinator.RefreshAuthoritativeAsync(stoppingToken);
            Interlocked.Exchange(ref incidentOpen, 0);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            LogRefreshFailure(logger, operation, exception);
            if (Interlocked.Exchange(ref incidentOpen, 1) == 0)
            {
                await ReportBestEffortAsync(exception, operation, stoppingToken);
            }
        }
    }

    private async Task ReportBestEffortAsync(
        Exception exception,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await incidentReporter.ReportAsync(
                new Incident(
                    exception,
                    nameof(RfqReadModelWorker),
                    Operation: operation),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception reportException)
        {
            LogIncidentFailure(logger, reportException);
        }
    }
}
