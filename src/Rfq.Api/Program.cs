using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Rfq.Api;
using Rfq.Application;
using Rfq.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
bool generatingOpenApi =
    Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

builder.Services.AddControllers(options => { })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(
    options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(item => item.Value?.Errors.Count > 0)
                .ToDictionary(
                    item => item.Key,
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
if (!generatingOpenApi)
{
    builder.Services.AddRfqApplication();
    builder.Services.AddRfqInfrastructure(builder.Configuration);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, DevelopmentCurrentUser>();
    builder.Services.AddSingleton<IIncidentReporter, LoggingIncidentReporter>();
    builder.Services.AddHostedService<QuoteExpiryWorker>();
}

WebApplication app = builder.Build();

if (!generatingOpenApi)
{
    app.UseMiddleware<ApiErrorMiddleware>();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

public partial class Program;
