using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Memory.Application;
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
        services.AddScoped<IResourceSourceSnapshotProvider, ResourceSourceSnapshotProvider>();
        services.AddMemorySourceGatewayAdapter<ResourceMemorySourceGatewayAdapter>();
        return services;
    }
}

public static class ResourcesModuleAssemblyMarker;


