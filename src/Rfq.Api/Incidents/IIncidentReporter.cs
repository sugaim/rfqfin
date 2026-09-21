namespace Rfq.Api;

public interface IIncidentReporter
{
    Task ReportAsync(
        Incident incident,
        CancellationToken cancellationToken = default);
}

public sealed record Incident(
    Exception Exception,
    string Source,
    string? TraceId = null,
    string? Operation = null);
