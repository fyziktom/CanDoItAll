using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Workflows.Runtime;

/// <summary>
/// Registers the provider-neutral workflow LLM invocation stack: the stateless
/// <see cref="ILlmInvocationPort"/> and the ordinary workflow LLM node
/// invoker. This composition is owned by the Workflows runtime — no MAF
/// adapter type participates; the port never constructs agents, sessions,
/// tools, memory, or workspace authority.
/// </summary>
public static class WorkflowLlmServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowLlmInvocation(
        this IServiceCollection services,
        ServiceLifetime invokerLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The lightweight port is stateless and only depends on the (already
        // singleton) provider runtime descriptor store/pool, so it is always a
        // singleton regardless of the invoker lifetime.
        services.TryAddSingleton<ILlmInvocationPort>(serviceProvider => new ProviderBackedLlmInvocationAdapter(
            serviceProvider.GetRequiredService<IProviderRuntimeDescriptorStore>(),
            serviceProvider.GetRequiredService<IProviderRuntimePool>()));
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IWorkflowLlmComponentInvoker),
            typeof(WorkflowLlmComponentInvoker),
            invokerLifetime));
        return services;
    }
}
