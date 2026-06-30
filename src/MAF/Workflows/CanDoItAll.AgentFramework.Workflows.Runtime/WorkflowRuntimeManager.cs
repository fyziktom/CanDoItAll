using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowRuntimeManager : IWorkflowRuntimeManager
{
    private readonly IReadOnlyDictionary<WorkflowRuntimeBackendKind, IWorkflowExecutionBackend> backends;
    private readonly IWorkflowRunStore store;
    private readonly IWorkflowEventSink eventSink;

    public WorkflowRuntimeManager(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        IWorkflowEventSink? eventSink = null)
    {
        ArgumentNullException.ThrowIfNull(backends);
        ArgumentNullException.ThrowIfNull(store);

        this.backends = backends.ToDictionary(item => item.Descriptor.Kind);
        this.store = store;
        this.eventSink = eventSink ?? new NullWorkflowEventSink();
    }

    public async Task<WorkflowRunSnapshot> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        var runId = WorkflowRunId.New();
        var requestedBackend = request.RequestedBackend ?? definition.RuntimePolicy.PreferredBackend;
        var now = DateTimeOffset.UtcNow;
        if (requestedBackend == WorkflowRuntimeBackendKind.InProcess &&
            definition.RuntimePolicy.RequireDurableProductionRuns &&
            !definition.RuntimePolicy.AllowInProcessPreviewRuns)
        {
            throw WorkflowRuntimeFailureDiagnosticMapper.CreateDurableBackendRequiredException(
                definition,
                requestedBackend,
                $"Workflow '{definition.Id}' requires a durable production runtime and does not allow in-process preview runs.");
        }

        if (!backends.TryGetValue(requestedBackend, out var backend))
        {
            throw WorkflowRuntimeFailureDiagnosticMapper.CreateBackendUnavailableException(
                definition,
                requestedBackend,
                $"Workflow runtime backend '{requestedBackend}' is not registered. Configure a backend explicitly instead of falling back silently.");
        }

        var result = await backend.StartAsync(definition, request, runId, cancellationToken);
        await PersistResultAsync(result, cancellationToken);
        return result.Run;
    }

    public Task<WorkflowRunSnapshot?> GetRunAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        return store.GetRunAsync(runId, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
        WorkflowId? workflowId = null,
        CancellationToken cancellationToken = default)
    {
        return store.ListRunsAsync(workflowId, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        return store.ListEventsAsync(runId, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        return store.ListCheckpointsAsync(runId, cancellationToken);
    }

    public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
        WorkflowEventPageRequest request,
        CancellationToken cancellationToken = default)
    {
        return store.ListEventPageAsync(request, cancellationToken);
    }

    public async Task<WorkflowRunSnapshot> CancelAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        var run = await store.GetRunAsync(runId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow run '{runId}' was not found.");
        if (run.State is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled)
        {
            return run;
        }

        var now = DateTimeOffset.UtcNow;
        var cancelled = run with
        {
            State = WorkflowRunState.Cancelled,
            Summary = "Workflow run was cancelled.",
            UpdatedAtUtc = now
        };
        var diagnostic = WorkflowRuntimeFailureDiagnosticMapper.Cancelled(cancelled, now);
        var eventRecord = new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.Cancelled,
            NodeId: null,
            cancelled.Summary,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.Runtime,
                "WorkflowCancelled",
                inlineJson: WorkflowRuntimeFailureDiagnosticMapper.Serialize(diagnostic)),
            now);

        await store.SaveRunAsync(cancelled, cancellationToken);
        await PublishAndStoreEventAsync(eventRecord, cancellationToken);
        return cancelled;
    }

    public async Task<WorkflowRunSnapshot> RespondToExternalRequestAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);

        var request = await store.GetExternalRequestAsync(requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow external request '{requestId}' was not found.");
        if (request.RespondedAtUtc.HasValue)
        {
            throw new InvalidOperationException($"Workflow external request '{requestId}' has already been answered.");
        }

        var run = await store.GetRunAsync(request.RunId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow run '{request.RunId}' was not found.");
        if (run.State != WorkflowRunState.WaitingForInput)
        {
            throw new InvalidOperationException($"Workflow run '{run.RunId}' is not waiting for external input.");
        }

        var now = DateTimeOffset.UtcNow;
        var answered = request with
        {
            ResponseJson = responseJson,
            RespondedAtUtc = now
        };
        var responseOutcome = ResolveExternalRequestResponseOutcome(request, responseJson);
        var updatedRun = run with
        {
            State = responseOutcome.State,
            Summary = responseOutcome.Summary,
            UpdatedAtUtc = now
        };
        var diagnosticJson = responseOutcome.EventKind == WorkflowEventKind.Error
            ? WorkflowRuntimeFailureDiagnosticMapper.Serialize(
                WorkflowRuntimeFailureDiagnosticMapper.ApprovalDenied(updatedRun, request, updatedRun.Summary, now))
            : string.Empty;
        var eventRecord = new WorkflowEventRecord(
            Guid.NewGuid(),
            run.RunId,
            responseOutcome.EventKind,
            request.NodeId,
            updatedRun.Summary,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.ExternalRequest,
                "WorkflowExternalRequestResponse",
                request.NodeId,
                requestId: request.Id,
                requestKind: request.Kind,
                inlineJson: string.IsNullOrWhiteSpace(diagnosticJson) ? responseJson : diagnosticJson),
            now);

        await store.SaveExternalRequestAsync(answered, cancellationToken);
        await store.SaveRunAsync(updatedRun, cancellationToken);
        await PublishAndStoreEventAsync(eventRecord, cancellationToken);
        return updatedRun;
    }

    private static WorkflowExternalRequestResponseOutcome ResolveExternalRequestResponseOutcome(
        WorkflowExternalRequestRecord request,
        string responseJson)
    {
        if (request.Kind is not (WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval))
        {
            return new WorkflowExternalRequestResponseOutcome(
                WorkflowRunState.Completed,
                WorkflowEventKind.Completed,
                $"Workflow external request '{request.EventName}' was answered.");
        }

        WorkflowExternalApprovalResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<WorkflowExternalApprovalResponse>(
                responseJson,
                WorkflowExternalRequestJson.Options);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "Workflow approval response JSON must be an object with an approved boolean property.",
                nameof(responseJson),
                exception);
        }

        if (response?.Approved is null)
        {
            throw new ArgumentException(
                "Workflow approval response JSON must be an object with an approved boolean property.",
                nameof(responseJson));
        }

        if (response.Approved.Value)
        {
            return new WorkflowExternalRequestResponseOutcome(
                WorkflowRunState.Completed,
                WorkflowEventKind.Completed,
                $"Workflow approval request '{request.EventName}' was approved.");
        }

        var reason = string.IsNullOrWhiteSpace(response.Message)
            ? string.Empty
            : $" Reason: {WorkflowExecutorRedaction.RedactText(response.Message)}";
        return new WorkflowExternalRequestResponseOutcome(
            WorkflowRunState.Failed,
            WorkflowEventKind.Error,
            $"Workflow approval request '{request.EventName}' was denied.{reason}");
    }

    private sealed record WorkflowExternalApprovalResponse(bool? Approved, string? Message);

    private sealed record WorkflowExternalRequestResponseOutcome(
        WorkflowRunState State,
        WorkflowEventKind EventKind,
        string Summary);

    private async Task PersistResultAsync(
        WorkflowBackendStartResult result,
        CancellationToken cancellationToken)
    {
        await store.SaveRunAsync(result.Run, cancellationToken);
        foreach (var workflowEvent in result.Events)
        {
            await PublishAndStoreEventAsync(workflowEvent, cancellationToken);
        }

        foreach (var request in result.ExternalRequests)
        {
            await store.SaveExternalRequestAsync(request, cancellationToken);
        }

        foreach (var checkpoint in result.Checkpoints)
        {
            await store.SaveCheckpointAsync(checkpoint, cancellationToken);
        }

        foreach (var artifact in result.Artifacts)
        {
            await store.SaveArtifactAsync(artifact, cancellationToken);
        }
    }

    private async Task PublishAndStoreEventAsync(
        WorkflowEventRecord workflowEvent,
        CancellationToken cancellationToken)
    {
        await store.SaveEventAsync(workflowEvent, cancellationToken);
        await eventSink.PublishAsync(workflowEvent, cancellationToken);
    }
}
