using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowExecutorCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<WorkflowExecutorContributionSet>();
        services.TryAddScoped<IWorkflowExecutorCatalog>(serviceProvider =>
            WorkflowExecutorCatalog.FromDescriptors(
                serviceProvider.GetRequiredService<WorkflowExecutorContributionSet>().Descriptors));
        services.TryAddScoped<IWorkflowExecutorRuntimeAvailabilityCatalog, WorkflowExecutorRuntimeAvailabilityCatalog>();
        services.TryAddScoped<IWorkflowExecutorExecutionObserver, CompositeWorkflowExecutorExecutionObserver>();
        services.TryAddScoped<IWorkflowExecutorInvoker>(serviceProvider =>
            new WorkflowExecutorInvoker(
                serviceProvider.GetRequiredService<IWorkflowExecutorCatalog>(),
                serviceProvider.GetRequiredService<WorkflowExecutorContributionSet>().ValidateImplementations(
                    serviceProvider.GetServices<IWorkflowExecutor>()),
                serviceProvider.GetService<IWorkflowExecutorExecutionObserver>(),
                serviceProvider.GetService<IWorkflowExecutorApprovalGate>()));

        return services;
    }
}
