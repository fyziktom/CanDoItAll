using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;

public static class StandardDocumentWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardDocumentWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, StandardDocumentWorkflowExecutorDescriptorSource>());
        services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IWorkflowExecutor), typeof(SpreadsheetWorkflowExecutor), executorLifetime));

        return services;
    }
}

public sealed class StandardDocumentWorkflowExecutorDescriptorSource : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
        => [BuiltInWorkflowExecutorDescriptors.Spreadsheet];
}
