using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control;

public static class StandardControlWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardControlWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, StandardControlWorkflowExecutorDescriptorSource>());
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IWorkflowExecutor), typeof(DelayWorkflowExecutor), executorLifetime));
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IWorkflowExecutor), typeof(HumanApprovalWorkflowExecutor), executorLifetime));
        foreach (var descriptor in BuiltInWorkflowExecutorDescriptors.Planned)
        {
            services.Add(ServiceDescriptor.Describe(
                typeof(IWorkflowExecutor),
                _ => new PlannedWorkflowExecutor(descriptor),
                executorLifetime));
        }

        return services;
    }
}

public sealed class StandardControlWorkflowExecutorDescriptorSource : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
        =>
        [
            BuiltInWorkflowExecutorDescriptors.Delay,
            BuiltInWorkflowExecutorDescriptors.ApprovalRequest,
            .. BuiltInWorkflowExecutorDescriptors.Planned
        ];
}
