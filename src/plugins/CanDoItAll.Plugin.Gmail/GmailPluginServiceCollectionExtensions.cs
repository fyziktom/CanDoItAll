using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Plugins;

public static class GmailPluginServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllGmailPlugin(
        this IServiceCollection services,
        bool registerBundledDescriptor = true,
        bool registerWorkflowExecutors = true)
    {
        if (registerBundledDescriptor)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ICanDoItAllPlugin, GmailBundledPlugin>());
        }

        services.AddScoped<GmailApiClient>();
        if (registerWorkflowExecutors)
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, GmailDownloadByLabelWorkflowExecutor>());
            services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, GmailMarkProcessedWorkflowExecutor>());
        }

        return services;
    }
}

public sealed class GmailPluginAssemblyMarker;

public sealed class GmailRuntimePluginServiceRegistrar : IRuntimePluginServiceRegistrar
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCanDoItAllGmailPlugin(registerBundledDescriptor: false, registerWorkflowExecutors: false);
    }
}
