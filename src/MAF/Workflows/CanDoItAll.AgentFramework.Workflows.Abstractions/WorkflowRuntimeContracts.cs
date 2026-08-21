using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed record WorkflowCompilationResult(
    bool Succeeded,
    string RuntimeDefinitionKey,
    WorkflowValidationResult Validation,
    string ErrorMessage)
{
    public static WorkflowCompilationResult Failed(
        WorkflowValidationResult validation,
        string errorMessage) => new(
        Succeeded: false,
        RuntimeDefinitionKey: string.Empty,
        Validation: validation,
        ErrorMessage: errorMessage);
}

public sealed record WorkflowBackendStartResult(
    WorkflowRunSnapshot Run,
    IReadOnlyList<WorkflowEventRecord> Events,
    IReadOnlyList<WorkflowExternalRequestRecord> ExternalRequests,
    IReadOnlyList<WorkflowArtifactRecord> Artifacts)
{
    public IReadOnlyList<WorkflowCheckpointRecord> Checkpoints { get; init; } = [];

    public IReadOnlyList<WorkflowUsageObservation> UsageObservations { get; init; } = [];
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

public interface IWorkflowExternalResponseBackend
{
    Task<WorkflowBackendStartResult> ResumeAsync(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        string responseJson,
        CancellationToken cancellationToken = default);

    Task<WorkflowBackendStartResult> ResumeAsync(
        WorkflowBackendResumeRequest request,
        CancellationToken cancellationToken = default)
        => ResumeAsync(
            request.Run,
            request.ExternalRequest,
            request.Response.GetRawText(),
            cancellationToken);
}

public sealed record WorkflowBackendResumeRequest
{
    public WorkflowBackendResumeRequest(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord externalRequest,
        JsonElement response)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(externalRequest);
        Run = run;
        ExternalRequest = externalRequest;
        Response = response;
        InvocationGeneration = externalRequest.Version.Value;
    }

    public WorkflowBackendResumeRequest(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord externalRequest,
        JsonElement response,
        WorkflowExternalResponseOperationId causationOperationId,
        long invocationGeneration,
        WorkflowExternalResponseAuthorization authorization)
        : this(run, externalRequest, response)
    {
        if (invocationGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(invocationGeneration),
                "Workflow backend invocation generation cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(authorization);
        CausationOperationId = causationOperationId;
        InvocationGeneration = invocationGeneration;
        Authorization = authorization;
    }

    public WorkflowRunSnapshot Run { get; init; }

    public WorkflowExternalRequestRecord ExternalRequest { get; init; }

    public JsonElement Response { get; init; }

    public WorkflowExternalResponseOperationId? CausationOperationId { get; init; }

    public long InvocationGeneration { get; init; }

    public WorkflowExternalResponseAuthorization? Authorization { get; init; }
}

public enum WorkflowRunCancellationOutcome
{
    CancellationRequested,
    AlreadyTerminal,
    NotFound,
    NotActive,
    BackendNotCancellable,
    TransitionRejected
}

public sealed record WorkflowRunCancellationResult(
    WorkflowRunCancellationOutcome Outcome,
    WorkflowRunSnapshot? Run,
    string Message)
{
    public bool Succeeded => Outcome == WorkflowRunCancellationOutcome.CancellationRequested;
}

public enum WorkflowExternalResponseOutcome
{
    Accepted,
    UnsupportedResume,
    AlreadyResponded,
    ResponseRejected,
    RequestNotFound,
    RunNotFound,
    RunNotWaiting,
    BackendUnavailable,
    ResumeFailed,
    TransitionRejected
}

public sealed record WorkflowExternalResponseResult(
    WorkflowExternalResponseOutcome Outcome,
    WorkflowRunSnapshot? Run,
    WorkflowExternalRequestRecord? Request,
    string Message)
{
    public bool Succeeded => Outcome == WorkflowExternalResponseOutcome.Accepted;
}

public sealed record WorkflowRunTransitionResult(
    bool Transitioned,
    WorkflowRunSnapshot? Run);

public enum WorkflowExternalResponseAcceptanceOutcome
{
    Accepted,
    NotFound,
    AlreadyResponded
}

public sealed record WorkflowExternalResponseAcceptanceResult(
    WorkflowExternalResponseAcceptanceOutcome Outcome,
    WorkflowExternalRequestRecord? Request);

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

public interface IWorkflowRunStore : IWorkflowCheckpointStore
{
    Task CreateRunWithStartedEventAsync(
        WorkflowRunSnapshot run,
        WorkflowEventRecord startedEvent,
        CancellationToken cancellationToken = default);

    Task<WorkflowRunTransitionResult> TryTransitionRunAsync(
        WorkflowRunId runId,
        IReadOnlyCollection<WorkflowRunState> expectedStates,
        WorkflowRunSnapshot updatedRun,
        WorkflowEventRecord? transitionEvent = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseAcceptanceResult> TryAcceptExternalResponseAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        DateTimeOffset respondedAtUtc,
        CancellationToken cancellationToken = default);

    Task SaveRunAsync(
        WorkflowRunSnapshot run,
        CancellationToken cancellationToken = default);

    Task<WorkflowRunSnapshot?> GetRunAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
        WorkflowId? workflowId = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
        WorkflowRunPageRequest request,
        CancellationToken cancellationToken = default);

    Task SaveEventAsync(
        WorkflowEventRecord workflowEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
        WorkflowEventPageRequest request,
        CancellationToken cancellationToken = default);

    Task SaveExternalRequestAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);

    Task SaveArtifactAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowRunPageRequest(
    WorkflowId? WorkflowId = null,
    WorkflowRunState? State = null,
    WorkflowRuntimeBackendKind? Backend = null,
    string Search = "",
    int PageIndex = 0,
    int PageSize = 10,
    WorkflowVersionId? VersionId = null,
    bool IncludeTotalCount = true)
{
    public IReadOnlyList<Guid> ProjectIds { get; init; } = [];

    public IReadOnlyList<WorkflowRunState> States { get; init; } = [];

    public DateTimeOffset? UpdatedFromUtc { get; init; }

    public DateTimeOffset? UpdatedToUtc { get; init; }
}

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
    Task PublishAsync(
        WorkflowEventRecord workflowEvent,
        CancellationToken cancellationToken = default);
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
