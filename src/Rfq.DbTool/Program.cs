using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rfq.Infrastructure;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    var command = args.FirstOrDefault();
    if (command is null or "--help" or "-h")
    {
        return ShowHelp();
    }

    if (command is not ("migrate" or "seed" or "reset-dev"))
    {
        return UnknownCommand(command);
    }

    try
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
        builder.Services.AddRfqInfrastructure(builder.Configuration);

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var operations = scope.ServiceProvider.GetRequiredService<DatabaseOperations>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        switch (command)
        {
            case "migrate":
                await operations.MigrateAsync();
                Console.WriteLine("Database migrations applied.");
                break;
            case "seed":
                await operations.SeedAsync();
                Console.WriteLine("Development seed applied.");
                break;
            case "reset-dev":
                await operations.ResetDevelopmentAsync(environment.EnvironmentName);
                Console.WriteLine("Development database reset, migrated, and seeded.");
                break;
        }

        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
}

static int ShowHelp()
{
    Console.WriteLine("RFQ database tool");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run --project src/Rfq.DbTool -- <command>");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  migrate    Apply pending database migrations");
    Console.WriteLine("  seed       Seed development/demo data idempotently");
    Console.WriteLine("  reset-dev  Reset, migrate, and seed the guarded local development database");
    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Use --help to list available commands.");
    return 1;
}
