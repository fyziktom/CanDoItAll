using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public static class PluginsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddPluginsModule(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICanDoItAllPlugin, DockerBundledPlugin>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPluginCatalogSource, BundledPluginCatalogSource>());
        services.AddScoped<PluginInstallationStore>();
        services.AddScoped<PluginGrantStore>();
        services.AddScoped<PluginConnectionStore>();
        services.AddScoped<PluginGrantEvaluator>();
        services.AddScoped<PluginSettingsService>();
        services.AddScoped<PluginCatalogService>();
        services.AddScoped<IPluginHostToolService, DockerHostToolService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, DockerListContainersWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, DockerPullImageWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, DockerStartContainerWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, DockerReadLogsWorkflowExecutor>());
        return services;
    }
}

public static class PluginsModuleAssemblyMarker;
