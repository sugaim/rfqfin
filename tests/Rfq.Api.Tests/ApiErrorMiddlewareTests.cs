using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Rfq.Api.Tests;

public sealed class ApiErrorMiddlewareTests
{
    [Fact]
    public async Task Unknown_exception_returns_generic_500_without_internal_message()
    {
        const string internalMessage = "password=secret; server=internal-db";
        var response = await InvokeAsync(new Exception(internalMessage));

        Assert.Equal(StatusCodes.Status500InternalServerError, response.Status);
        Assert.Equal("InternalServerError", response.Code);
        Assert.Equal("An unexpected error occurred.", response.Detail);
        Assert.DoesNotContain(internalMessage, response.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Known_exception_keeps_status_code_and_detail_mapping()
    {
        const string detail = "The requested RFQ was not found.";
        var response = await InvokeAsync(new KeyNotFoundException(detail));

        Assert.Equal(StatusCodes.Status404NotFound, response.Status);
        Assert.Equal("NotFound", response.Code);
        Assert.Equal(detail, response.Detail);
    }

    private static async Task<ErrorResponse> InvokeAsync(Exception exception)
    {
        var middleware = new ApiErrorMiddleware(
            _ => Task.FromException(exception),
            NullLogger<ApiErrorMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new ErrorResponse(
            context.Response.StatusCode,
            root.GetProperty("code").GetString(),
            root.GetProperty("detail").GetString(),
            json);
    }

    private sealed record ErrorResponse(int Status, string? Code, string? Detail, string Json);
}
