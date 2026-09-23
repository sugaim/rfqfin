using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
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
            bool isExpected = exception is ExpectedRfqException expected
                && TryMap(expected.Kind, out mapped);
            (int status, string code) = isExpected
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

            string detail = !isExpected
                ? "An unexpected error occurred."
                : exception.Message;
            var problem = new ProblemDetails
            {
                Status = status,
                Title = code,
                Detail = detail,
                Extensions =
                {
                    ["code"] = code,
                    ["traceId"] = context.TraceIdentifier,
                },
            };
            if (isExpected && exception is CalculationFailureException calculationFailure)
            {
                problem.Extensions["calculationErrorCode"] = calculationFailure.Code;
                problem.Extensions["failureLogId"] = calculationFailure.FailureLogId;
            }

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static bool TryMap(RfqErrorKind kind, out (int Status, string Code) mapped)
    {
        switch (kind)
        {
            case RfqErrorKind.Validation:
                mapped = (StatusCodes.Status400BadRequest, "Validation");
                return true;
            case RfqErrorKind.InvalidState:
                mapped = (StatusCodes.Status409Conflict, "InvalidState");
                return true;
            case RfqErrorKind.VersionConflict:
                mapped = (StatusCodes.Status409Conflict, "VersionConflict");
                return true;
            case RfqErrorKind.NotFound:
                mapped = (StatusCodes.Status404NotFound, "NotFound");
                return true;
            case RfqErrorKind.Forbidden:
                mapped = (StatusCodes.Status403Forbidden, "Forbidden");
                return true;
            case RfqErrorKind.CalculationFailure:
                mapped = (StatusCodes.Status422UnprocessableEntity, "CalculationFailure");
                return true;
            case RfqErrorKind.ServiceUnavailable:
                mapped = (StatusCodes.Status503ServiceUnavailable, "ServiceUnavailable");
                return true;
            default:
                mapped = default;
                return false;
        }
    }
}
