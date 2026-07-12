using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;

public static class StandardTransformWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardTransformWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkflowExecutorContribution<JsonTransformWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.JsonTransform, executorLifetime);
        services.AddWorkflowExecutorContribution<MarkdownRenderWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.MarkdownRender, executorLifetime);

        return services;
    }
}
