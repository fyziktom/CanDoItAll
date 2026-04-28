using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Components.BaseLib;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllBaseLib(this IServiceCollection services)
    {
        services.AddScoped<DialogService>();
        services.AddScoped<TooltipService>();
        services.AddScoped<NotificationService>();
        return services;
    }
}
