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

    Task<WorkflowDefinition> SaveDefinitionAsync(
        WorkflowDefinitionSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
        WorkflowDefinitionStatusChangeRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowValidationResult> ValidateDefinitionAsync(
        WorkflowDefinition definition,
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

    Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<WorkflowRunSnapshot> CancelAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowFailureDiagnosticSink
{
    Task PublishAsync(
        WorkflowFailureDiagnosticEnvelope diagnostic,
        CancellationToken cancellationToken = default);
}
