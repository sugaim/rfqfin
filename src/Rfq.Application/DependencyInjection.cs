using Microsoft.Extensions.DependencyInjection;

namespace Rfq.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRfqApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateDraft>();
        services.AddScoped<GetActiveSalesRfqs>();

        return services;
    }
}
