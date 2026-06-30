using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

public static class StandardWorkspaceWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardWorkspaceWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, StandardWorkspaceWorkflowExecutorDescriptorSource>());
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IWorkflowExecutor), typeof(WorkspaceFileWorkflowExecutor), executorLifetime));
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IWorkflowExecutor), typeof(SourceIngestionWorkflowExecutor), executorLifetime));

        return services;
    }
}

public sealed class StandardWorkspaceWorkflowExecutorDescriptorSource : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
        =>
        [
            BuiltInWorkflowExecutorDescriptors.StorageFile,
            BuiltInWorkflowExecutorDescriptors.SourceIngestion
        ];
}
