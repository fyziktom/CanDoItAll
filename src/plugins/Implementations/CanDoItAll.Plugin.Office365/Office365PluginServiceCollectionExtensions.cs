using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Plugins;

public static class Office365PluginServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllOffice365Plugin(
        this IServiceCollection services,
        bool registerBundledDescriptor = true,
        bool registerWorkflowExecutors = true)
    {
        if (registerBundledDescriptor)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ICanDoItAllPlugin, Office365BundledPlugin>());
        }

        services.AddScoped<Office365GraphClient>();
        services.TryAddScoped<IOffice365WorkflowClient>(serviceProvider =>
            serviceProvider.GetRequiredService<Office365GraphClient>());
        if (registerWorkflowExecutors)
        {
            services.AddWorkflowExecutorContribution<Office365DownloadByCategoryWorkflowExecutor>(Office365WorkflowExecutorDescriptors.DownloadByCategory, ServiceLifetime.Scoped);
            services.AddWorkflowExecutorContribution<Office365DownloadByAddressWorkflowExecutor>(Office365WorkflowExecutorDescriptors.DownloadByAddress, ServiceLifetime.Scoped);
            services.AddWorkflowExecutorContribution<Office365MarkProcessedWorkflowExecutor>(Office365WorkflowExecutorDescriptors.MarkProcessed, ServiceLifetime.Scoped);
        }

        return services;
    }
}

public sealed class Office365PluginAssemblyMarker;

public sealed class Office365RuntimePluginServiceRegistrar : IRuntimePluginServiceRegistrar
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCanDoItAllOffice365Plugin(registerBundledDescriptor: false, registerWorkflowExecutors: false);
    }
}
