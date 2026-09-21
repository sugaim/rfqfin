using Microsoft.Extensions.DependencyInjection;

namespace Rfq.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRfqApplication(this IServiceCollection services)
    {
        services.AddSingleton<IRfqAuthorization, RfqAuthorization>();
        services.AddScoped<CreateDraft>();
        services.AddScoped<GetActiveSalesRfqs>();
        services.AddScoped<ResolveRfqDefaults>();
        services.AddScoped<AssignedTraderValidator>();
        services.AddScoped<InitialRfqFactory>();
        services.AddScoped<UpdateInitialDraft>();
        services.AddScoped<ConfirmInitialDraft>();
        services.AddScoped<ConfirmNewRfq>();
        services.AddScoped<DiscardInitialDraft>();
        services.AddScoped<GetActiveTraderRfqs>();
        services.AddScoped<PickUpRfq>();
        services.AddScoped<ReleaseRfq>();
        services.AddScoped<AssignTrader>();
        services.AddScoped<TakeOverRfq>();

        return services;
    }
}
