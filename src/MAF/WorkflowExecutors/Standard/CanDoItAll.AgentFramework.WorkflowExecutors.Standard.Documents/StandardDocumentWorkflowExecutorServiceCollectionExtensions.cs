using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;

public static class StandardDocumentWorkflowExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddStandardDocumentWorkflowExecutors(
        this IServiceCollection services,
        ServiceLifetime executorLifetime)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWorkflowExecutorContribution<SpreadsheetWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.Spreadsheet, executorLifetime);
        services.AddWorkflowExecutorContribution<DocumentToMarkdownWorkflowExecutor>(BuiltInWorkflowExecutorDescriptors.DocumentToMarkdown, executorLifetime);

        return services;
    }
}
