namespace Rfq.Api;

public sealed partial class LoggingIncidentReporter(
    ILogger<LoggingIncidentReporter> logger) : IIncidentReporter
{
    public Task ReportAsync(
        Incident incident,
        CancellationToken cancellationToken = default)
    {
        LogUnexpectedFailure(
            logger,
            incident.Exception,
            incident.Source,
            incident.TraceId,
            incident.Operation);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unexpected failure. Source={Source} TraceId={TraceId} Operation={Operation}")]
    private static partial void LogUnexpectedFailure(
        ILogger logger,
        Exception exception,
        string source,
        string? traceId,
        string? operation);
}
