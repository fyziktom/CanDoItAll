using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;

public static class StandardProjectStructureWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardProjectStructureWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAdd(ServiceDescriptor.Describe(typeof(IProjectStructureRuntimeGateway), typeof(UnavailableProjectStructureRuntimeGateway), executorLifetime));
        services.AddWorkflowExecutorContribution<ProjectStructureWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.ProjectStructure, executorLifetime);

        return services;
    }
}
