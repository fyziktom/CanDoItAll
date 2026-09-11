using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

public static class StandardWorkspaceWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardWorkspaceWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkflowExecutorContribution<WorkspaceFileWorkflowExecutor>(WorkspaceFileWorkflowExecutor.DisclosureDescriptor, executorLifetime);
        services.AddWorkflowExecutorContribution<SourceIngestionWorkflowExecutor>(SourceIngestionWorkflowExecutor.DisclosureDescriptor, executorLifetime);

        return services;
    }
}
