using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Runtime;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Maf;

public static class MafWorkflowAdapterServiceCollectionExtensions
{
    public static IServiceCollection AddMafWorkflowAdapterServices(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddStandardWorkflowExecutors(executorLifetime);
        services.AddWorkflowExecutorCoreServices();
        // Neutral workflow LLM invocation (stateless port + invoker) is owned
        // by the provider-neutral Workflows runtime; the MAF adapter only
        // composes it alongside its own MAF-specific services.
        services.AddWorkflowLlmInvocation(executorLifetime);
        services.AddWorkflowCoreServices();
        services.AddWorkflowRuntimeServices();
        services.TryAddScoped<MafWorkflowCompiler>();
        services.TryAddScoped<IWorkflowMafCompiler>(serviceProvider => serviceProvider.GetRequiredService<MafWorkflowCompiler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutionBackend, MafInProcessWorkflowExecutionBackend>());

        return services;
    }
}
