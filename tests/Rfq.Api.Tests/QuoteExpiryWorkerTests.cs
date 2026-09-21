using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rfq.Application;
using Rfq.Domain;
using Xunit;

namespace Rfq.Api.Tests;

public sealed class QuoteExpiryWorkerTests
{
    [Fact]
    public async Task Unexpected_candidate_failure_is_reported()
    {
        var expected = new InvalidOperationException("repository contract failure");
        var reporter = new IncidentReporter();
        using var provider = Services(
            new ExpiryQueries([Candidate()]), new ThrowingCases(expected));
        var worker = Worker(provider.GetRequiredService<IServiceScopeFactory>(), reporter);

        await worker.RunScanBoundaryAsync(CancellationToken.None);

        var incident = Assert.Single(reporter.Incidents);
        Assert.Same(expected, incident.Exception);
        Assert.Equal("QuoteExpiryWorker", incident.Source);
    }

    [Fact]
    public async Task Shutdown_cancellation_is_not_reported()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reporter = new IncidentReporter();
        using var provider = Services(
            new CancellingExpiryQueries(), new ThrowingCases(new Exception("unused")));
        var worker = Worker(provider.GetRequiredService<IServiceScopeFactory>(), reporter);

        await worker.RunScanBoundaryAsync(cancellation.Token);

        Assert.Empty(reporter.Incidents);
    }

    [Fact]
    public async Task Reporter_failure_does_not_escape_worker_boundary()
    {
        using var provider = Services(
            new ExpiryQueries([]), new ThrowingCases(new Exception("unused")));
        var worker = Worker(provider.GetRequiredService<IServiceScopeFactory>(),
            new IncidentReporter(shouldThrow: true));

        await worker.ReportBestEffortAsync(new Exception("scan failed"));
    }

    private static ServiceProvider Services(
        IQuoteExpiryQueries queries,
        IRfqCaseRepository cases)
    {
        var services = new ServiceCollection();
        services.AddSingleton(queries);
        services.AddSingleton(cases);
        services.AddSingleton<IQuoteEventSink, QuoteEvents>();
        services.AddSingleton<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(TimeProvider.System);
        services.AddTransient<ExpireQuote>();
        return services.BuildServiceProvider();
    }

    private static QuoteExpiryWorker Worker(
        IServiceScopeFactory scopeFactory,
        IIncidentReporter reporter) => new(
        scopeFactory,
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["QuoteExpiry:IntervalSeconds"] = "1" }).Build(),
        reporter);

    private static ExpiredQuoteCandidate Candidate() => new(
        new CaseId(1), QuoteId.New(), new StateVersion(1));

    private sealed class ExpiryQueries(IReadOnlyList<ExpiredQuoteCandidate> candidates)
        : IQuoteExpiryQueries
    {
        public Task<IReadOnlyList<ExpiredQuoteCandidate>> GetExpiredAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken = default) => Task.FromResult(candidates);
    }

    private sealed class CancellingExpiryQueries : IQuoteExpiryQueries
    {
        public Task<IReadOnlyList<ExpiredQuoteCandidate>> GetExpiredAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            Task.FromCanceled<IReadOnlyList<ExpiredQuoteCandidate>>(cancellationToken);
    }

    private sealed class ThrowingCases(Exception exception) : IRfqCaseRepository
    {
        public void Add(RfqCase rfqCase) => throw new NotSupportedException();
        public Task<RfqCase?> GetAsync(CaseId caseId, CancellationToken cancellationToken = default) =>
            Task.FromException<RfqCase?>(exception);
        public void Update(RfqCase rfqCase) => throw new NotSupportedException();
        public void UpdateRevision(RfqRevision revision) => throw new NotSupportedException();
    }

    private sealed class QuoteEvents : IQuoteEventSink
    {
        public void Record(QuoteTransition transition) { }
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public void DiscardChanges() { }
    }

    private sealed class IncidentReporter(bool shouldThrow = false) : IIncidentReporter
    {
        public List<Incident> Incidents { get; } = [];
        public Task ReportAsync(Incident incident, CancellationToken cancellationToken = default)
        {
            Incidents.Add(incident);
            return shouldThrow
                ? Task.FromException(new Exception("reporter failed"))
                : Task.CompletedTask;
        }
    }
}
