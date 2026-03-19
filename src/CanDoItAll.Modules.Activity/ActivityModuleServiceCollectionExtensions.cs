using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Activity;

public static class ActivityModuleServiceCollectionExtensions
{
    public static IServiceCollection AddActivityModule(this IServiceCollection services)
    {
        services.AddScoped<ActivityService>();
        services.AddScoped<CanDoItAll.SharedKernel.IActivityStream>(serviceProvider => serviceProvider.GetRequiredService<ActivityService>());
        return services;
    }
}

public static class ActivityModuleAssemblyMarker;
