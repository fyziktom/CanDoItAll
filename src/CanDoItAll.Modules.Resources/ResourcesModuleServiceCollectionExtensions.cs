using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Resources;

public static class ResourcesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddResourcesModule(this IServiceCollection services)
    {
        services.AddScoped<ResourcesService>();
        return services;
    }
}

public static class ResourcesModuleAssemblyMarker;


