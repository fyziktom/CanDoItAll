using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control;

public static class StandardControlWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardControlWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkflowExecutorContribution<DelayWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.Delay, executorLifetime);
        services.AddWorkflowExecutorContribution<HumanApprovalWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.ApprovalRequest, executorLifetime);
        foreach (var descriptor in BuiltInWorkflowExecutorDescriptors.Planned)
        {
            services.AddWorkflowExecutorDescriptorContribution(descriptor);
        }

        return services;
    }
}
