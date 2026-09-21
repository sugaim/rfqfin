using Rfq.Application;

namespace Rfq.Api;

public sealed class QuoteExpiryWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IIncidentReporter incidentReporter) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Max(1, configuration.GetValue("QuoteExpiry:IntervalSeconds", 10));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunScanBoundaryAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    internal async Task RunScanBoundaryAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ScanAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            await ReportBestEffortAsync(exception);
        }
    }

    internal async Task ScanAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var expiryQueries = scope.ServiceProvider.GetRequiredService<IQuoteExpiryQueries>();
        var useCase = scope.ServiceProvider.GetRequiredService<ExpireQuote>();
        foreach (var candidate in await expiryQueries.GetExpiredAsync(
            DateTimeOffset.UtcNow, stoppingToken))
        {
            await useCase.ExecuteAsync(candidate, stoppingToken);
        }
    }

    internal async Task ReportBestEffortAsync(Exception exception)
    {
        try
        {
            await incidentReporter.ReportAsync(new Incident(
                exception, "QuoteExpiryWorker", Operation: "ScanExpiredQuotes"));
        }
        catch
        {
            // Incident delivery is best-effort and must not terminate the worker loop.
        }
    }
}
