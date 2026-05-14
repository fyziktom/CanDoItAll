using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Plugins;

public static class DockerPluginServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllDockerPlugin(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICanDoItAllPlugin, DockerBundledPlugin>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPluginHostToolRecipeCatalogSource, DockerHostToolRecipeCatalogSource>());
        services.AddScoped<IPluginHostToolService, DockerHostToolService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, DockerListContainersWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, DockerPullImageWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, DockerStartContainerWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, DockerReadLogsWorkflowExecutor>());
        return services;
    }
}

public sealed class DockerPluginAssemblyMarker;

public sealed class DockerRuntimePluginServiceRegistrar : IRuntimePluginServiceRegistrar
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCanDoItAllDockerPlugin();
    }
}

internal sealed class DockerHostToolRecipeCatalogSource : IPluginHostToolRecipeCatalogSource
{
    private static readonly PluginHostToolRecipeDescriptor[] DockerRecipes =
    [
        new(PluginHostToolRecipeIds.DockerListContainers, "List Docker containers", "Read Docker container metadata through docker ps.", PluginGrantRiskKind.High, MutatesHost: false),
        new(PluginHostToolRecipeIds.DockerPullImage, "Pull Docker image", "Pull a Docker image through a constrained docker pull recipe.", PluginGrantRiskKind.High, MutatesHost: true),
        new(PluginHostToolRecipeIds.DockerStartContainer, "Start Docker container", "Start or create a Docker container through a constrained docker run/start recipe.", PluginGrantRiskKind.High, MutatesHost: true),
        new(PluginHostToolRecipeIds.DockerReadLogs, "Read Docker logs", "Read bounded Docker container logs.", PluginGrantRiskKind.High, MutatesHost: false)
    ];

    public IReadOnlyList<PluginHostToolRecipeDescriptor> ListForPlugin(PluginCatalogItem catalogItem)
        => catalogItem.Capabilities.HasFlag(PluginCapabilityKind.HostCommand) &&
           catalogItem.PluginId == DockerPluginConstants.PluginId
            ? DockerRecipes
            : [];
}
