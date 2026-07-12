using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Memory.Context;
using CanDoItAll.AgentFramework.Memory.Tools;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Memory.DependencyInjection;

public static class AgentFrameworkMemoryServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, MemoryAgentRuntimeToolProvider>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAgentContextContributor, MemoryAgentContextContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, MemoryWorkflowExecutorDescriptorSource>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IWorkflowExecutor, MemoryWorkflowExecutor>());
        return services;
    }
}
