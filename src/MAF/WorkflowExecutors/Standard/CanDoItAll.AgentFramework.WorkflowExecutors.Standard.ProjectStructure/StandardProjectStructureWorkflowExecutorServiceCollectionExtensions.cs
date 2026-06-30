using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
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
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, StandardProjectStructureWorkflowExecutorDescriptorSource>());
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IWorkflowExecutor), typeof(ProjectStructureWorkflowExecutor), executorLifetime));

        return services;
    }
}

public sealed class StandardProjectStructureWorkflowExecutorDescriptorSource : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
        => [BuiltInWorkflowExecutorDescriptors.ProjectStructure];
}
