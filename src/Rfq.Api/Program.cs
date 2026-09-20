using Rfq.Application;
using Rfq.Api;
using Rfq.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddRfqApplication();
builder.Services.AddRfqInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ICurrentUser, DevelopmentCurrentUser>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

public partial class Program;
