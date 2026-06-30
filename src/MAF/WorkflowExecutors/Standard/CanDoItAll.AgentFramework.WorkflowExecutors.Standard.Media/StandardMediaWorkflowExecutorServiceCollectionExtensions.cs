using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
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
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, StandardMediaWorkflowExecutorDescriptorSource>());
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IWorkflowExecutor), typeof(ImageGenerationWorkflowExecutor), executorLifetime));

        return services;
    }
}

public sealed class StandardMediaWorkflowExecutorDescriptorSource : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
        => [BuiltInWorkflowExecutorDescriptors.ImageGeneration];
}
