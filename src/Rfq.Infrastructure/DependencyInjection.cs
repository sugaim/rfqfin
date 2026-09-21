using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rfq.Application;

namespace Rfq.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRfqInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(PostgreSqlDatabaseConfiguration.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{PostgreSqlDatabaseConfiguration.ConnectionStringName}' is not configured.");

        services.AddDbContext<RfqDbContext>(options =>
            PostgreSqlDatabaseConfiguration.Configure(options, connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICaseIdGenerator, PostgreSqlCaseIdGenerator>();
        services.AddScoped<IRfqCaseRepository, RfqCaseRepository>();
        services.AddScoped<IActiveRfqQueries, PostgreSqlActiveRfqQueries>();
        services.AddScoped<IQuoteExpiryQueries, PostgreSqlQuoteExpiryQueries>();
        services.AddScoped<IWorkingQuoteRepository, WorkingQuoteRepository>();
        services.AddScoped<IConfirmedQuoteRepository, ConfirmedQuoteRepository>();
        services.AddScoped<ICaseMemoRepository, CaseMemoRepository>();
        services.AddScoped<PersistedEventSink>();
        services.AddScoped<IQuoteEventSink>(provider => provider.GetRequiredService<PersistedEventSink>());
        services.AddScoped<IRfqEventSink>(provider => provider.GetRequiredService<PersistedEventSink>());
        services.AddSingleton<ICalculationClient, MockCalculationClient>();
        services.AddScoped<ISecuritySearch, PostgreSqlSecuritySearch>();
        services.AddScoped<IClientSearch, PostgreSqlClientSearch>();
        services.AddScoped<IUserDirectory, PostgreSqlUserDirectory>();
        services.AddScoped<ICategoryRouting, PostgreSqlCategoryRouting>();
        services.AddScoped<ISystemDateProvider, PostgreSqlSystemDateProvider>();
        services.AddScoped<IBusinessDateResolver, PostgreSqlBusinessDateResolver>();
        services.AddScoped<IEventFeed, PostgreSqlEventFeed>();
        services.AddScoped<IOperationalQueries, PostgreSqlOperationalQueries>();
        services.AddSingleton<IStandardSettlementResolver, MockStandardSettlementResolver>();
        services.AddScoped<IUnitOfWork, PostgreSqlUnitOfWork>();
        services.AddScoped<DevelopmentDataSeeder>();
        services.AddScoped<DatabaseOperations>();

        return services;
    }
}
