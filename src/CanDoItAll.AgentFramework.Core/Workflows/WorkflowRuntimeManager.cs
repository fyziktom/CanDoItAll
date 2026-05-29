using System.Collections.Concurrent;
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
            throw new InvalidOperationException(
                $"Workflow '{definition.Id}' requires a durable production runtime and does not allow in-process preview runs.");
        }

        if (!backends.TryGetValue(requestedBackend, out var backend))
        {
            throw new InvalidOperationException(
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
        var eventRecord = new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.Cancelled,
            NodeId: null,
            cancelled.Summary,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.Runtime,
                "WorkflowCancelled"),
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
                inlineJson: responseJson),
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

public sealed class InMemoryWorkflowRunStore :
    IWorkflowRunStore,
    IWorkflowArtifactStore,
    IWorkflowExternalRequestStore
{
    private readonly ConcurrentDictionary<WorkflowRunId, WorkflowRunSnapshot> runs = new();
    private readonly ConcurrentDictionary<WorkflowRunId, ConcurrentQueue<WorkflowEventRecord>> events = new();
    private readonly ConcurrentDictionary<WorkflowExternalRequestId, WorkflowExternalRequestRecord> requests = new();
    private readonly ConcurrentDictionary<WorkflowRunId, ConcurrentQueue<WorkflowExternalRequestId>> requestsByRun = new();
    private readonly ConcurrentDictionary<WorkflowRunId, ConcurrentQueue<WorkflowArtifactRecord>> artifacts = new();
    private readonly ConcurrentDictionary<WorkflowCheckpointId, WorkflowCheckpointRecord> checkpoints = new();
    private readonly ConcurrentDictionary<WorkflowRunId, ConcurrentQueue<WorkflowCheckpointId>> checkpointsByRun = new();

    public Task SaveRunAsync(WorkflowRunSnapshot run, CancellationToken cancellationToken = default)
    {
        runs[run.RunId] = run;
        return Task.CompletedTask;
    }

    public Task<WorkflowRunSnapshot?> GetRunAsync(WorkflowRunId runId, CancellationToken cancellationToken = default)
    {
        runs.TryGetValue(runId, out var run);
        return Task.FromResult(run);
    }

    public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
        WorkflowId? workflowId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = runs.Values
            .Where(run => workflowId is null || run.WorkflowId == workflowId.Value)
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ToArray();
        return Task.FromResult<IReadOnlyList<WorkflowRunSnapshot>>(snapshot);
    }

    public Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
        WorkflowRunPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var pageIndex = NormalizePageIndex(request.PageIndex);
        var pageSize = NormalizePageSize(request.PageSize);
        var filtered = runs.Values.AsEnumerable();
        if (request.WorkflowId.HasValue)
        {
            filtered = filtered.Where(run => run.WorkflowId == request.WorkflowId.Value);
        }

        if (request.State.HasValue)
        {
            filtered = filtered.Where(run => run.State == request.State.Value);
        }

        if (request.Backend.HasValue)
        {
            filtered = filtered.Where(run => run.Backend == request.Backend.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            filtered = filtered.Where(run =>
                run.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                run.BackendRunId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                run.RunId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ToArray();
        var items = ordered
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult(new WorkflowListPage<WorkflowRunSnapshot>(
            items,
            pageIndex,
            pageSize,
            ordered.Length));
    }

    public Task SaveEventAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken = default)
    {
        events.GetOrAdd(workflowEvent.RunId, _ => new ConcurrentQueue<WorkflowEventRecord>())
            .Enqueue(workflowEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        if (!events.TryGetValue(runId, out var queue))
        {
            return Task.FromResult<IReadOnlyList<WorkflowEventRecord>>([]);
        }

        return Task.FromResult<IReadOnlyList<WorkflowEventRecord>>(queue.ToArray());
    }

    public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
        WorkflowEventPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var pageIndex = NormalizePageIndex(request.PageIndex);
        var pageSize = NormalizePageSize(request.PageSize);
        if (!events.TryGetValue(request.RunId, out var queue))
        {
            return Task.FromResult(new WorkflowListPage<WorkflowEventRecord>([], pageIndex, pageSize, 0));
        }

        var ordered = queue
            .OrderBy(item => item.CreatedAtUtc)
            .ToArray();
        var items = ordered
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult(new WorkflowListPage<WorkflowEventRecord>(
            items,
            pageIndex,
            pageSize,
            ordered.Length));
    }

    public Task<WorkflowCheckpointRecord> SaveCheckpointAsync(
        WorkflowCheckpointRecord checkpoint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (checkpoints.TryAdd(checkpoint.Id, checkpoint))
        {
            checkpointsByRun.GetOrAdd(checkpoint.RunId, _ => new ConcurrentQueue<WorkflowCheckpointId>())
                .Enqueue(checkpoint.Id);
        }
        else
        {
            checkpoints[checkpoint.Id] = checkpoint;
        }

        return Task.FromResult(checkpoint);
    }

    public Task<WorkflowCheckpointRecord?> GetCheckpointAsync(
        WorkflowCheckpointId checkpointId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        checkpoints.TryGetValue(checkpointId, out var checkpoint);
        return Task.FromResult(checkpoint);
    }

    public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!checkpointsByRun.TryGetValue(runId, out var ids))
        {
            return Task.FromResult<IReadOnlyList<WorkflowCheckpointRecord>>([]);
        }

        var records = ids
            .Select(id => checkpoints.TryGetValue(id, out var checkpoint) ? checkpoint : null)
            .OfType<WorkflowCheckpointRecord>()
            .OrderBy(checkpoint => checkpoint.CreatedAtUtc)
            .ThenBy(checkpoint => checkpoint.Id.Value)
            .ToArray();
        return Task.FromResult<IReadOnlyList<WorkflowCheckpointRecord>>(records);
    }

    public Task<WorkflowCheckpointRecord> MarkCheckpointResumedAsync(
        WorkflowCheckpointId checkpointId,
        DateTimeOffset resumedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!checkpoints.TryGetValue(checkpointId, out var checkpoint))
        {
            throw new KeyNotFoundException($"Workflow checkpoint '{checkpointId}' was not found.");
        }

        var resumed = checkpoint with
        {
            ResumedAtUtc = resumedAtUtc
        };
        checkpoints[checkpointId] = resumed;
        return Task.FromResult(resumed);
    }

    public Task SaveExternalRequestAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken = default)
    {
        requests[request.Id] = request;
        requestsByRun.GetOrAdd(request.RunId, _ => new ConcurrentQueue<WorkflowExternalRequestId>())
            .Enqueue(request.Id);
        return Task.CompletedTask;
    }

    public Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken = default)
    {
        requests.TryGetValue(requestId, out var request);
        return Task.FromResult(request);
    }

    public Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        if (!requestsByRun.TryGetValue(runId, out var ids))
        {
            return Task.FromResult<IReadOnlyList<WorkflowExternalRequestRecord>>([]);
        }

        var pending = new List<WorkflowExternalRequestRecord>();
        foreach (var id in ids)
        {
            if (requests.TryGetValue(id, out var request) && !request.RespondedAtUtc.HasValue)
            {
                pending.Add(request);
            }
        }

        return Task.FromResult<IReadOnlyList<WorkflowExternalRequestRecord>>(pending);
    }

    Task<IReadOnlyList<WorkflowExternalRequestRecord>> IWorkflowExternalRequestStore.ListPendingRequestsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken)
    {
        return ListPendingExternalRequestsAsync(runId, cancellationToken);
    }

    public Task<WorkflowExternalRequestRecord> SaveRequestAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken = default)
    {
        return SaveExternalRequestCoreAsync(request);
    }

    public Task<WorkflowExternalRequestRecord> MarkRespondedAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        DateTimeOffset respondedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!requests.TryGetValue(requestId, out var request))
        {
            throw new KeyNotFoundException($"Workflow external request '{requestId}' was not found.");
        }

        var answered = request with
        {
            ResponseJson = responseJson,
            RespondedAtUtc = respondedAtUtc
        };
        requests[requestId] = answered;
        return Task.FromResult(answered);
    }

    public Task SaveArtifactAsync(WorkflowArtifactRecord artifact, CancellationToken cancellationToken = default)
    {
        artifacts.GetOrAdd(artifact.RunId, _ => new ConcurrentQueue<WorkflowArtifactRecord>())
            .Enqueue(artifact);
        return Task.CompletedTask;
    }

    Task<WorkflowArtifactRecord> IWorkflowArtifactStore.SaveArtifactAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken)
    {
        return SaveArtifactCoreAsync(artifact);
    }

    public Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        if (!artifacts.TryGetValue(runId, out var queue))
        {
            return Task.FromResult<IReadOnlyList<WorkflowArtifactRecord>>([]);
        }

        return Task.FromResult<IReadOnlyList<WorkflowArtifactRecord>>(queue.ToArray());
    }

    private Task<WorkflowExternalRequestRecord> SaveExternalRequestCoreAsync(WorkflowExternalRequestRecord request)
    {
        requests[request.Id] = request;
        requestsByRun.GetOrAdd(request.RunId, _ => new ConcurrentQueue<WorkflowExternalRequestId>())
            .Enqueue(request.Id);
        return Task.FromResult(request);
    }

    private Task<WorkflowArtifactRecord> SaveArtifactCoreAsync(WorkflowArtifactRecord artifact)
    {
        artifacts.GetOrAdd(artifact.RunId, _ => new ConcurrentQueue<WorkflowArtifactRecord>())
            .Enqueue(artifact);
        return Task.FromResult(artifact);
    }

    private static int NormalizePageIndex(int pageIndex)
        => Math.Max(0, pageIndex);

    private static int NormalizePageSize(int pageSize)
        => Math.Clamp(pageSize, 1, 100);
}

public sealed class NullWorkflowEventSink : IWorkflowEventSink
{
    public Task PublishAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
