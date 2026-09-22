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
        services.AddRfqDatabaseOperations(configuration);
        services.AddScoped<ICaseIdGenerator, PostgreSqlCaseIdGenerator>();
        services.AddScoped<IRfqCaseRepository, RfqCaseRepository>();
        services.AddScoped<ISalesRfqQueries, EfCoreSalesRfqQueries>();
        services.AddScoped<ISalesRecentRevisionQueries, EfCoreSalesRecentRevisionQueries>();
        services.AddScoped<ITraderRfqQueries, EfCoreTraderRfqQueries>();
        services.AddScoped<IQuoteExpiryQueries, EfCoreQuoteExpiryQueries>();
        services.AddScoped<IWorkingQuoteRepository, WorkingQuoteRepository>();
        services.AddScoped<IConfirmedQuoteRepository, ConfirmedQuoteRepository>();
        services.AddScoped<IRfqMemoRepository, RfqMemoRepository>();
        services.AddScoped<PersistedEventSink>();
        services.AddScoped<IQuoteEventSink>(provider => provider.GetRequiredService<PersistedEventSink>());
        services.AddScoped<IRfqEventSink>(provider => provider.GetRequiredService<PersistedEventSink>());
        services.AddSingleton<ICalculationClient, MockCalculationClient>();
        services.AddScoped<ISecuritySearch, PostgreSqlSecuritySearch>();
        services.AddScoped<IClientSearch, PostgreSqlClientSearch>();
        services.AddScoped<IUserDirectory, PostgreSqlUserDirectory>();
        services.AddScoped<IUserCandidateQueries, EfCoreUserCandidateQueries>();
        services.AddScoped<IQuoteExpirySettings, EfCoreQuoteExpirySettings>();
        services.AddScoped<IQuoteModeSettings, EfCoreQuoteModeSettings>();
        services.AddScoped<IThemeSettings, EfCoreThemeSettings>();
        services.AddScoped<ICategoryRouting, EfCoreCategoryRouting>();
        services.AddScoped<IBusinessDateProvider, EfCoreBusinessDateProvider>();
        services.AddScoped<IDeskLocalDateResolver, EfCoreDeskLocalDateResolver>();
        services.AddScoped<IEventFeed, EfCoreEventFeed>();
        services.AddScoped<IRfqSearchQueries, EfCoreRfqSearchQueries>();
        services.AddScoped<IRfqRevisionQueries, EfCoreRfqRevisionQueries>();
        services.AddScoped<IRfqQuoteQueries, EfCoreRfqQuoteQueries>();
        services.AddScoped<IEodQueries, EfCoreEodQueries>();
        services.AddScoped<IPostProcessQueries, EfCorePostProcessQueries>();
        services.AddScoped<IPostProcessVisibility, EfCorePostProcessVisibility>();
        services.AddScoped<IGridConfigStore, EfCoreGridConfigStore>();
        services.AddSingleton<IStandardSettlementResolver, MockStandardSettlementResolver>();
        services.AddScoped<IUnitOfWork, PostgreSqlUnitOfWork>();

        return services;
    }

    public static IServiceCollection AddRfqDatabaseOperations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(PostgreSqlDatabaseConfiguration.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{PostgreSqlDatabaseConfiguration.ConnectionStringName}' is not configured.");

        services.AddDbContext<RfqDbContext>(options =>
            PostgreSqlDatabaseConfiguration.Configure(options, connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DevelopmentDataSeeder>();
        services.AddScoped<DatabaseOperations>();

        return services;
    }
}
