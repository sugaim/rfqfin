using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Rfq.Application;
using Rfq.Domain;
using Xunit;

namespace Rfq.Api.Tests;

public sealed class ApiErrorMiddlewareTests
{
    public static TheoryData<Exception, int, string> ExpectedErrors => new()
    {
        { new RfqRequestValidationException("validation"), 400, "Validation" },
        { new DomainRuleViolationException("state"), 409, "InvalidState" },
        { new StateVersionMismatchException("version"), 409, "VersionConflict" },
        { new RfqNotFoundException("missing"), 404, "NotFound" },
        { new RfqForbiddenException("forbidden"), 403, "Forbidden" },
        {
            new CalculationFailureException(Guid.NewGuid(), "CALC", "calculation"),
            422,
            "CalculationFailure" },
        { new RfqReadModelUnavailableException("unavailable"), 503, "ServiceUnavailable" },
    };

    public static TheoryData<Exception> UnexpectedErrors =>
    [
        new DomainInvariantException("domain invariant"),
        new RfqInvariantException("system invariant"),
        new InvalidOperationException("invalid operation"),
        new ArgumentException("argument"),
        new KeyNotFoundException("key"),
        new UnauthorizedAccessException("unauthorized"),
        new Exception("unknown"),
    ];

    [Theory]
    [MemberData(nameof(ExpectedErrors))]
    public async Task Expected_error_kind_maps_to_http_contract(
        Exception exception, int status, string code)
    {
        var reporter = new IncidentReporter();
        ErrorResponse response = await InvokeAsync(exception, reporter);

        Assert.Equal(status, response.Status);
        Assert.Equal(code, response.Code);
        Assert.Equal(exception.Message, response.Detail);
        Assert.Equal("trace-07", response.TraceId);
        Assert.Empty(reporter.Incidents);
    }

    [Theory]
    [MemberData(nameof(UnexpectedErrors))]
    public async Task Unexpected_and_bcl_exceptions_return_generic_500_and_report_once(
        Exception exception)
    {
        var reporter = new IncidentReporter();
        ErrorResponse response = await InvokeAsync(exception, reporter);

        Assert.Equal(StatusCodes.Status500InternalServerError, response.Status);
        Assert.Equal("InternalServerError", response.Code);
        Assert.Equal("An unexpected error occurred.", response.Detail);
        Assert.Equal("trace-07", response.TraceId);
        Assert.DoesNotContain(exception.Message, response.Json, StringComparison.Ordinal);
        Incident incident = Assert.Single(reporter.Incidents);
        Assert.Same(exception, incident.Exception);
        Assert.Equal(response.TraceId, incident.TraceId);
        Assert.Equal("POST /api/rfqs/1", incident.Operation);
    }

    [Fact]
    public async Task Reporter_failure_does_not_replace_original_500_response()
    {
        ErrorResponse response = await InvokeAsync(
            new InvalidOperationException("internal"),
            new IncidentReporter(shouldThrow: true));

        Assert.Equal(StatusCodes.Status500InternalServerError, response.Status);
        Assert.Equal("An unexpected error occurred.", response.Detail);
    }

    [Fact]
    public async Task Calculation_failure_includes_structured_diagnostics()
    {
        var failureLogId = Guid.NewGuid();

        ErrorResponse response = await InvokeAsync(
            new CalculationFailureException(failureLogId, "CALC-42", "calculation"),
            new IncidentReporter());

        using var document = JsonDocument.Parse(response.Json);
        Assert.Equal(
            "CALC-42",
            document.RootElement
                .GetProperty("calculationErrorCode").GetString());
        Assert.Equal(
            failureLogId.ToString(),
            document.RootElement
                .GetProperty("failureLogId").GetString());
        Assert.Equal("trace-07", document.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Unmapped_error_kind_is_unexpected_and_reported()
    {
        var exception = new UnmappedExpectedException();
        var reporter = new IncidentReporter();

        ErrorResponse response = await InvokeAsync(exception, reporter);

        Assert.Equal(StatusCodes.Status500InternalServerError, response.Status);
        Assert.Equal("InternalServerError", response.Code);
        Assert.Equal("An unexpected error occurred.", response.Detail);
        Assert.DoesNotContain(exception.Message, response.Json, StringComparison.Ordinal);
        Assert.Same(exception, Assert.Single(reporter.Incidents).Exception);
    }

    private static async Task<ErrorResponse> InvokeAsync(
        Exception exception,
        IIncidentReporter reporter)
    {
        var middleware = new ApiErrorMiddleware(
            _ => Task.FromException(exception), reporter);
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-07"
        };
        context.Request.Method = "POST";
        context.Request.Path = "/api/rfqs/1";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        string json = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new ErrorResponse(
            context.Response.StatusCode,
            root.GetProperty("code").GetString(),
            root.GetProperty("detail").GetString(),
            root.GetProperty("traceId").GetString(),
            json);
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

    private sealed class UnmappedExpectedException()
        : ExpectedRfqException((RfqErrorKind)int.MaxValue, "unmapped expected error");

    private sealed record ErrorResponse(
        int Status,
        string? Code,
        string? Detail,
        string? TraceId,
        string Json);
}
