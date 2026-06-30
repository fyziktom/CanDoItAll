using CanDoItAll.AgentFramework.Core;
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
