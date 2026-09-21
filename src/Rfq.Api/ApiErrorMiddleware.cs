using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api;

public sealed class ApiErrorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception) when (IsExpected(exception))
        {
            var (status, code) = exception switch
            {
                CalculationFailureException => (422, "CalculationFailure"),
                UnauthorizedAccessException => (403, "Forbidden"),
                KeyNotFoundException => (404, "NotFound"),
                InvalidOperationException => (409, "Conflict"),
                _ => (400, "Validation"),
            };
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = code,
                Detail = exception.Message,
                Extensions = { ["code"] = code },
            });
        }
    }

    private static bool IsExpected(Exception exception) => exception is
        ArgumentException or InvalidOperationException or UnauthorizedAccessException
        or KeyNotFoundException or CalculationFailureException;
}
