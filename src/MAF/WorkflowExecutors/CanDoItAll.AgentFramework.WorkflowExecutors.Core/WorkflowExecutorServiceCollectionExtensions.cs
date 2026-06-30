using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowExecutorCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IWorkflowExecutorCatalog>(serviceProvider =>
            WorkflowExecutorCatalog.FromDescriptorSources(serviceProvider.GetServices<IWorkflowExecutorDescriptorSource>()));
        services.TryAddScoped<IWorkflowExecutorExecutionObserver, CompositeWorkflowExecutorExecutionObserver>();
        services.TryAddScoped<IWorkflowExecutorInvoker, WorkflowExecutorInvoker>();

        return services;
    }
}
