namespace Rfq.Api;

public sealed class LoggingIncidentReporter(
    ILogger<LoggingIncidentReporter> logger) : IIncidentReporter
{
    public Task ReportAsync(
        Incident incident,
        CancellationToken cancellationToken = default)
    {
        logger.LogError(
            incident.Exception,
            "Unexpected failure. Source={Source} TraceId={TraceId} Operation={Operation}",
            incident.Source,
            incident.TraceId,
            incident.Operation);
        return Task.CompletedTask;
    }
}
