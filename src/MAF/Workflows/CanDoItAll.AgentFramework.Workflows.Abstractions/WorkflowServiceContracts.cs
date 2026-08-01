using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowCatalogService
{
    Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
        WorkflowId workflowId,
        WorkflowLifecycleStatus status,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinition> SaveDefinitionAsync(
        WorkflowDefinitionSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
        WorkflowDefinitionStatusChangeRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinition> ImportDefinitionAsync(
        WorkflowDefinitionImportRequest request,
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
    Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(
        CancellationToken cancellationToken = default);

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

public interface IWorkflowDefinitionValidator
{
    WorkflowValidationResult Validate(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components);
}

public interface IWorkflowRuntimeBackendCatalog
{
    IReadOnlyList<WorkflowRuntimeBackendDescriptor> ListBackends();

    WorkflowRuntimeBackendDescriptor GetRequiredBackend(WorkflowRuntimeBackendKind backend);
}

public interface IWorkflowRuntimeManager
{
    Task<WorkflowRunSnapshot> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowRunSnapshot?> GetRunAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
        WorkflowId? workflowId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
        WorkflowEventPageRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowRunSnapshot> CancelAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<WorkflowRunCancellationResult> RequestCancellationAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<WorkflowRunSnapshot> RespondToExternalRequestAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseResult> SubmitExternalResponseAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowFailureDiagnosticSink
{
    Task PublishAsync(
        WorkflowFailureDiagnosticEnvelope diagnostic,
        CancellationToken cancellationToken = default);
}
