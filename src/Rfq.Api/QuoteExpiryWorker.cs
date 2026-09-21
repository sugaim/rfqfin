using Rfq.Application;

namespace Rfq.Api;

public sealed class QuoteExpiryWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<QuoteExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Max(1, configuration.GetValue("QuoteExpiry:IntervalSeconds", 10));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var expiryQueries = scope.ServiceProvider.GetRequiredService<IQuoteExpiryQueries>();
                var useCase = scope.ServiceProvider.GetRequiredService<ExpireQuote>();
                foreach (var candidate in await expiryQueries.GetExpiredAsync(
                    DateTimeOffset.UtcNow, stoppingToken))
                {
                    try { await useCase.ExecuteAsync(candidate, stoppingToken); }
                    catch (InvalidOperationException) { /* raced with a normal command */ }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Quote expiry scan failed.");
            }
        }
    }
}
