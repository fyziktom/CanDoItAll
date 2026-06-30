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

    Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
        WorkflowEventPageRequest request,
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
    IReadOnlyList<WorkflowArtifactRecord> Artifacts)
{
    public IReadOnlyList<WorkflowCheckpointRecord> Checkpoints { get; init; } = [];
}

public interface IWorkflowExecutionBackend
{
    WorkflowRuntimeBackendDescriptor Descriptor { get; }

    Task<WorkflowBackendStartResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowCheckpointStore
{
    Task<WorkflowCheckpointRecord> SaveCheckpointAsync(
        WorkflowCheckpointRecord checkpoint,
        CancellationToken cancellationToken = default);

    Task<WorkflowCheckpointRecord?> GetCheckpointAsync(
        WorkflowCheckpointId checkpointId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<WorkflowCheckpointRecord> MarkCheckpointResumedAsync(
        WorkflowCheckpointId checkpointId,
        DateTimeOffset resumedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowCheckpointCreateRequest(
    WorkflowDefinition Definition,
    WorkflowRunId RunId,
    WorkflowRuntimeBackendKind Backend,
    WorkflowCheckpointKind Kind,
    DateTimeOffset CreatedAtUtc)
{
    public WorkflowNodeId? NodeId { get; init; }

    public WorkflowExternalRequestId? ExternalRequestId { get; init; }

    public string BackendCheckpointId { get; init; } = string.Empty;

    public string PayloadReference { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public interface IWorkflowCheckpointFactory
{
    WorkflowCheckpointRecord CreateMetadataCheckpoint(WorkflowCheckpointCreateRequest request);
}

public sealed class WorkflowCheckpointFactory : IWorkflowCheckpointFactory
{
    public const string MetadataOnlyPayloadReference = "runtime://metadata-only";

    public const string MetadataOnlyResumeUnavailableReason =
        "Resume is not available for metadata-only workflow checkpoints. Use a durable workflow backend with trusted runtime state before enabling resume.";

    public WorkflowCheckpointRecord CreateMetadataCheckpoint(WorkflowCheckpointCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Definition);

        var payloadReference = string.IsNullOrWhiteSpace(request.PayloadReference)
            ? MetadataOnlyPayloadReference
            : request.PayloadReference.Trim();
        var summary = string.IsNullOrWhiteSpace(request.Summary)
            ? $"Workflow checkpoint '{request.Kind}' captured."
            : request.Summary.Trim();

        return new WorkflowCheckpointRecord(
            WorkflowCheckpointId.New(),
            request.RunId,
            request.Definition.Id,
            request.Definition.VersionId,
            request.Backend,
            request.Kind,
            WorkflowCheckpointTrustBoundary.MetadataOnly,
            WorkflowResumeAvailability.NotSupported,
            request.NodeId,
            request.ExternalRequestId,
            request.BackendCheckpointId.Trim(),
            payloadReference,
            PayloadHash: string.Empty,
            summary,
            MetadataOnlyResumeUnavailableReason,
            request.CreatedAtUtc,
            ResumedAtUtc: null);
    }
}

public interface IWorkflowRunStore : IWorkflowCheckpointStore
{
    Task SaveRunAsync(WorkflowRunSnapshot run, CancellationToken cancellationToken = default);

    Task<WorkflowRunSnapshot?> GetRunAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(WorkflowId? workflowId = null, CancellationToken cancellationToken = default);

    Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
        WorkflowRunPageRequest request,
        CancellationToken cancellationToken = default);

    Task SaveEventAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);

    Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
        WorkflowEventPageRequest request,
        CancellationToken cancellationToken = default);

    Task SaveExternalRequestAsync(WorkflowExternalRequestRecord request, CancellationToken cancellationToken = default);

    Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(WorkflowExternalRequestId requestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);

    Task SaveArtifactAsync(WorkflowArtifactRecord artifact, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);
}

public sealed record WorkflowRunPageRequest(
    WorkflowId? WorkflowId = null,
    WorkflowRunState? State = null,
    WorkflowRuntimeBackendKind? Backend = null,
    string Search = "",
    int PageIndex = 0,
    int PageSize = 10);

public sealed record WorkflowEventPageRequest(
    WorkflowRunId RunId,
    int PageIndex = 0,
    int PageSize = 10);

public sealed record WorkflowListPage<T>(
    IReadOnlyList<T> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageIndex > 0;

    public bool HasNextPage => PageIndex + 1 < TotalPages;
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

public sealed record WorkflowArtifactContent(
    WorkflowArtifactRecord Artifact,
    string Content);

public interface IWorkflowArtifactContentStore
{
    Task SaveContentAsync(
        WorkflowArtifactRecord artifact,
        string content,
        CancellationToken cancellationToken = default);

    Task<WorkflowArtifactContent?> ReadContentAsync(
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
