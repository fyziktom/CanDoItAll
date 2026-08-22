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
        services.TryAddScoped<WorkflowExecutorInvoker>(serviceProvider =>
            new WorkflowExecutorInvoker(
                serviceProvider.GetRequiredService<IWorkflowExecutorCatalog>(),
                serviceProvider.GetRequiredService<WorkflowExecutorContributionSet>().ValidateImplementations(
                    serviceProvider.GetServices<IWorkflowExecutor>()),
                serviceProvider.GetService<IWorkflowExecutorExecutionObserver>(),
                serviceProvider.GetService<IWorkflowExecutorApprovalGate>(),
                serviceProvider.GetService<TimeProvider>()));
        services.TryAddScoped<IWorkflowExecutorInvoker>(serviceProvider =>
            serviceProvider.GetRequiredService<WorkflowExecutorInvoker>());

        return services;
    }

    public static IServiceCollection AddWorkflowExecutorInvocationDeduplication(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkflowExecutorCoreServices();
        services.Replace(ServiceDescriptor.Scoped<IWorkflowExecutorInvoker>(serviceProvider =>
            new DeduplicatingWorkflowExecutorInvoker(
                serviceProvider.GetRequiredService<WorkflowExecutorInvoker>(),
                serviceProvider.GetRequiredService<IWorkflowExecutorCatalog>(),
                serviceProvider.GetRequiredService<IWorkflowExecutorInvocationDeduplicationStore>(),
                serviceProvider.GetService<TimeProvider>())));

        return services;
    }
}
