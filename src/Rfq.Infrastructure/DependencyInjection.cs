using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Rfq.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRfqInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConfiguration.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{DatabaseConfiguration.ConnectionStringName}' is not configured.");

        services.AddDbContext<RfqDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DevelopmentDataSeeder>();
        services.AddScoped<DatabaseOperations>();

        return services;
    }
}
