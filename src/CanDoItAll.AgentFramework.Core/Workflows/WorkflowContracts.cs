using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record WorkflowCompilationResult(
    bool Succeeded,
    string RuntimeDefinitionKey,
    WorkflowValidationResult Validation,
    string ErrorMessage)
{
    public static WorkflowCompilationResult Failed(WorkflowValidationResult validation, string errorMessage) => new(
        Succeeded: false,
        RuntimeDefinitionKey: string.Empty,
        Validation: validation,
        ErrorMessage: errorMessage);
}

public interface IWorkflowDefinitionValidator
{
    WorkflowValidationResult Validate(WorkflowDefinition definition, IReadOnlyList<LlmCallComponent> components);
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

    Task<WorkflowRunSnapshot> RespondToExternalRequestAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowBackendStartResult(
    WorkflowRunSnapshot Run,
    IReadOnlyList<WorkflowEventRecord> Events,
    IReadOnlyList<WorkflowExternalRequestRecord> ExternalRequests,
    IReadOnlyList<WorkflowArtifactRecord> Artifacts);

public interface IWorkflowExecutionBackend
{
    WorkflowRuntimeBackendDescriptor Descriptor { get; }

    Task<WorkflowBackendStartResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowRunStore
{
    Task SaveRunAsync(WorkflowRunSnapshot run, CancellationToken cancellationToken = default);

    Task<WorkflowRunSnapshot?> GetRunAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(WorkflowId? workflowId = null, CancellationToken cancellationToken = default);

    Task SaveEventAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);

    Task SaveExternalRequestAsync(WorkflowExternalRequestRecord request, CancellationToken cancellationToken = default);

    Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(WorkflowExternalRequestId requestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);

    Task SaveArtifactAsync(WorkflowArtifactRecord artifact, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);
}

public interface IWorkflowEventSink
{
    Task PublishAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken = default);
}

public interface IWorkflowArtifactStore
{
    Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<WorkflowArtifactRecord> SaveArtifactAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowExternalRequestStore
{
    Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingRequestsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalRequestRecord> SaveRequestAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalRequestRecord> MarkRespondedAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        DateTimeOffset respondedAtUtc,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowProcessExecutorBridge
{
    Task<WorkflowRunSnapshot> StartForProcessAssignmentAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        CancellationToken cancellationToken = default);
}
