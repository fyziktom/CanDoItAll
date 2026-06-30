using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;

public static class StandardTransformWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardTransformWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, StandardTransformWorkflowExecutorDescriptorSource>());
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IWorkflowExecutor), typeof(JsonTransformWorkflowExecutor), executorLifetime));
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IWorkflowExecutor), typeof(MarkdownRenderWorkflowExecutor), executorLifetime));

        return services;
    }
}

public sealed class StandardTransformWorkflowExecutorDescriptorSource : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
        =>
        [
            BuiltInWorkflowExecutorDescriptors.JsonTransform,
            BuiltInWorkflowExecutorDescriptors.MarkdownRender
        ];
}
