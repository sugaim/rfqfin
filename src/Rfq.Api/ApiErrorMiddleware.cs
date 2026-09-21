using Microsoft.AspNetCore.Mvc;
using Rfq.Domain;

namespace Rfq.Api;

public sealed class ApiErrorMiddleware(
    RequestDelegate next,
    IIncidentReporter incidentReporter)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            var mapped = default((int Status, string Code));
            var isExpected = exception is ExpectedRfqException expected
                && TryMap(expected.Kind, out mapped);
            var (status, code) = isExpected
                ? mapped
                : (StatusCodes.Status500InternalServerError, "InternalServerError");
            if (!isExpected)
            {
                try
                {
                    await incidentReporter.ReportAsync(new Incident(
                        exception,
                        "ApiErrorMiddleware",
                        context.TraceIdentifier,
                        $"{context.Request.Method} {context.Request.Path}"));
                }
                catch
                {
                    // Incident delivery is best-effort and must not replace the original response.
                }
            }

            var detail = !isExpected
                ? "An unexpected error occurred."
                : exception.Message;
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = code,
                Detail = detail,
                Extensions =
                {
                    ["code"] = code,
                    ["traceId"] = context.TraceIdentifier,
                },
            });
        }
    }

    private static bool TryMap(RfqErrorKind kind, out (int Status, string Code) mapped)
    {
        mapped = kind switch
        {
            RfqErrorKind.Validation => (StatusCodes.Status400BadRequest, "Validation"),
            RfqErrorKind.InvalidState => (StatusCodes.Status409Conflict, "InvalidState"),
            RfqErrorKind.VersionConflict => (StatusCodes.Status409Conflict, "VersionConflict"),
            RfqErrorKind.NotFound => (StatusCodes.Status404NotFound, "NotFound"),
            RfqErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            RfqErrorKind.CalculationFailure =>
                (StatusCodes.Status422UnprocessableEntity, "CalculationFailure"),
            _ => default,
        };
        return Enum.IsDefined(kind);
    }
}
