using CanDoItAll.Modules.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Resources;

public static class ResourcesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddResourcesModule(this IServiceCollection services)
    {
        services.TryAddScoped<ConnectorPluginRegistry>();
        services.AddScoped<IResourceConnectorPlugin, WebhookResourceConnectorPlugin>();
        services.AddScoped<ResourceConnectorPluginRegistry>();
        services.AddScoped<IConnectorManifestSource>(serviceProvider => serviceProvider.GetRequiredService<ResourceConnectorPluginRegistry>());
        services.AddScoped<ResourcesService>();
        return services;
    }
}

public static class ResourcesModuleAssemblyMarker;


