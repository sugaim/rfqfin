using System.Text.Json.Serialization;
using Rfq.Application;
using Rfq.Api;
using Rfq.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddRfqApplication();
builder.Services.AddRfqInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, DevelopmentCurrentUser>();
builder.Services.AddScoped<IEventFeed, PostgreSqlEventFeed>();
builder.Services.AddScoped<IOperationalQueries, PostgreSqlOperationalQueries>();
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
