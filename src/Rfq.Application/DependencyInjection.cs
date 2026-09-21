using Microsoft.Extensions.DependencyInjection;

namespace Rfq.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRfqApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateDraft>();
        services.AddScoped<GetActiveSalesRfqs>();
        services.AddScoped<ResolveRfqDefaults>();
        services.AddScoped<AssignedTraderValidator>();
        services.AddScoped<InitialRfqFactory>();
        services.AddScoped<UpdateInitialDraft>();
        services.AddScoped<ConfirmInitialDraft>();
        services.AddScoped<ConfirmNewRfq>();
        services.AddScoped<DiscardInitialDraft>();

        return services;
    }
}
