using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;

public static class StandardNetworkWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardNetworkWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkflowExecutorContribution<HttpFetchWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.HttpFetch, executorLifetime);

        return services;
    }
}
