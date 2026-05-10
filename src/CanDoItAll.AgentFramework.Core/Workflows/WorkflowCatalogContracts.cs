using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkflowCatalogService
{
    Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinition> SaveDefinitionAsync(
        WorkflowDefinitionSaveRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteDefinitionAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default);

    Task<WorkflowValidationResult> ValidateDefinitionAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowSettingsService
{
    Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<WorkflowSettings> SaveSettingsAsync(
        WorkflowSettings settings,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowComponentLibraryService
{
    Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(CancellationToken cancellationToken = default);

    Task<LlmCallComponent?> GetComponentAsync(
        WorkflowComponentId componentId,
        CancellationToken cancellationToken = default);

    Task<LlmCallComponent> SaveComponentAsync(
        LlmCallComponentSaveRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteComponentAsync(
        WorkflowComponentId componentId,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowTestRunner
{
    Task<WorkflowTestRunResult> RunAsync(
        WorkflowTestRunRequest request,
        CancellationToken cancellationToken = default);
}
