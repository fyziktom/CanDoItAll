using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Plugins;

public static class PluginsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddPluginsModule(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPluginCatalogSource, BundledPluginCatalogSource>());
        services.AddScoped<PluginInstallationStore>();
        services.AddScoped<PluginCatalogService>();
        return services;
    }
}

public static class PluginsModuleAssemblyMarker;
