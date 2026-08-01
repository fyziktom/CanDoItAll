using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Memory.Application;
using CanDoItAll.FileTools.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Resources;

public static class ResourcesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddResourcesModule(this IServiceCollection services)
    {
        services.TryAddScoped<ConnectorPluginRegistry>();
        services.AddScoped<IResourceConnectorPlugin, WebhookResourceConnectorPlugin>();
        services.AddScoped<IResourceConnectorPlugin, StorageObjectResourceConnectorPlugin>();
        services.AddScoped<ResourceConnectorPluginRegistry>();
        services.AddScoped<IConnectorManifestSource>(serviceProvider => serviceProvider.GetRequiredService<ResourceConnectorPluginRegistry>());
        services.AddScoped<ResourcesService>();
        services.AddScoped<IResourceFileSourceCatalog, ResourceFileSourceCatalog>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IFileToolsStorageBindingSource, ResourceFileToolsStorageBindingSource>());
        services.AddScoped<ResourceFileBrowseCoordinator>();
        services.AddScoped<IStorageObjectResourceWriter, StorageObjectResourceWriter>();
        services.AddScoped<ResourceStorageObjectPromotionService>();
        services.AddScoped<ResourceStorageObjectInteractionService>();
        services.AddScoped<IResourceSourceSnapshotProvider, ResourceSourceSnapshotProvider>();
        services.AddMemorySourceGatewayAdapter<ResourceMemorySourceGatewayAdapter>();
        return services;
    }
}

public static class ResourcesModuleAssemblyMarker;


