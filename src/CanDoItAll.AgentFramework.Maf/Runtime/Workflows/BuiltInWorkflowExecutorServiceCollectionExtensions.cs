using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Maf;

public static class BuiltInWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddBuiltInWorkflowExecutors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IProjectStructureRuntimeGateway, UnavailableProjectStructureRuntimeGateway>();
        services.TryAddSingleton<IAgentImageGenerationService, UnavailableAgentImageGenerationService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, BuiltInWorkflowExecutorDescriptorSource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, WorkspaceFileWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, JsonTransformWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, MarkdownRenderWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, SourceIngestionWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, HttpFetchWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, DelayWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, HumanApprovalWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, SpreadsheetWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, ProjectStructureWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, ImageGenerationWorkflowExecutor>());

        foreach (var descriptor in BuiltInWorkflowExecutorDescriptors.Planned)
        {
            services.AddSingleton<IWorkflowExecutor>(new PlannedWorkflowExecutor(descriptor));
        }

        return services;
    }
}
