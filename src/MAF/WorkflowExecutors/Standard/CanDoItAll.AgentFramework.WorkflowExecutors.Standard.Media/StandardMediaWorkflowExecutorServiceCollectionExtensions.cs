using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;

public static class StandardMediaWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardMediaWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAdd(ServiceDescriptor.Describe(typeof(IAgentImageGenerationService), typeof(UnavailableAgentImageGenerationService), executorLifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IAgentImageAnalysisService), typeof(UnavailableAgentImageAnalysisService), executorLifetime));
        services.AddWorkflowExecutorContribution<ImageGenerationWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.ImageGeneration, executorLifetime);
        services.AddWorkflowExecutorContribution<ImageInspectWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.ImageInspect, executorLifetime);
        services.AddWorkflowExecutorContribution<ImageAnalyzeWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.ImageAnalyze, executorLifetime);

        return services;
    }
}
