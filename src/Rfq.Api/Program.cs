using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Api;
using Rfq.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => { })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(item => item.Key,
                item => item.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The request value is invalid."
                        : error.ErrorMessage).ToArray());
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation",
            Detail = "One or more request values are invalid.",
        };
        problem.Extensions["code"] = "Validation";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem);
    };
});
builder.Services.AddOpenApi();
builder.Services.AddRfqApplication();
builder.Services.AddRfqInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, DevelopmentCurrentUser>();
builder.Services.AddSingleton<IIncidentReporter, LoggingIncidentReporter>();
builder.Services.AddHostedService<QuoteExpiryWorker>();

var app = builder.Build();

app.UseMiddleware<ApiErrorMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

public partial class Program;
