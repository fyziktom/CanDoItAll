using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
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
        // The lightweight port is stateless and only depends on the (already singleton) provider runtime
        // descriptor store/pool registered by AddMafProviderRuntimeServices, so it is always a singleton
        // regardless of executorLifetime.
        services.TryAddSingleton<ILlmInvocationPort>(serviceProvider => new ProviderBackedLlmInvocationAdapter(
            serviceProvider.GetRequiredService<IProviderRuntimeDescriptorStore>(),
            serviceProvider.GetRequiredService<IProviderRuntimePool>()));
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IWorkflowLlmComponentInvoker),
            typeof(MafWorkflowLlmComponentInvoker),
            executorLifetime));
        services.AddWorkflowCoreServices();
        services.AddWorkflowRuntimeServices();
        services.TryAddScoped<MafWorkflowCompiler>();
        services.TryAddScoped<IWorkflowMafCompiler>(serviceProvider => serviceProvider.GetRequiredService<MafWorkflowCompiler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutionBackend, MafInProcessWorkflowExecutionBackend>());

        return services;
    }
}
