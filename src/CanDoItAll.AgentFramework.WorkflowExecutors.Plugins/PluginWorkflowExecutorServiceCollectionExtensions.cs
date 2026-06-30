using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public static class PluginWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddPluginWorkflowExecutorBoundary(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutorDescriptorSource, PluginWorkflowExecutorDescriptorSource>());
        return services;
    }
}
