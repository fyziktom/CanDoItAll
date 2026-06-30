using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard;

public static class StandardWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddStandardControlWorkflowExecutors(executorLifetime);
        services.AddStandardTransformWorkflowExecutors(executorLifetime);
        services.AddStandardWorkspaceWorkflowExecutors(executorLifetime);
        services.AddStandardNetworkWorkflowExecutors(executorLifetime);
        services.AddStandardDocumentWorkflowExecutors(executorLifetime);
        services.AddStandardMediaWorkflowExecutors(executorLifetime);
        services.AddStandardProjectStructureWorkflowExecutors(executorLifetime);

        return services;
    }
}
