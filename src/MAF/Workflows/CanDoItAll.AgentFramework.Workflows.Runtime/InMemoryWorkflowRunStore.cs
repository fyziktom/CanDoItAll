using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowRunStore :
    IWorkflowRunStore,
    IWorkflowArtifactStore,
    IWorkflowExternalRequestStore,
    IWorkflowOverviewStore
{
    private const int MaximumOverviewRecentTake = 12;
    private const int MaximumOverviewTopWorkflowTake = 10;

    private readonly object mutationSync = new();
    private readonly ConcurrentDictionary<WorkflowRunId, WorkflowRunSnapshot> runs = new();
    private readonly ConcurrentDictionary<WorkflowRunId, ConcurrentQueue<WorkflowEventRecord>> events = new();
    private readonly ConcurrentDictionary<WorkflowExternalRequestId, WorkflowExternalRequestRecord> requests = new();
    private readonly ConcurrentDictionary<WorkflowRunId, ConcurrentQueue<WorkflowExternalRequestId>> requestsByRun = new();
    private readonly ConcurrentDictionary<WorkflowRunId, ConcurrentQueue<WorkflowArtifactRecord>> artifacts = new();
    private readonly ConcurrentDictionary<WorkflowCheckpointId, WorkflowCheckpointRecord> checkpoints = new();
    private readonly ConcurrentDictionary<WorkflowRunId, ConcurrentQueue<WorkflowCheckpointId>> checkpointsByRun = new();

    public Task SaveRunAsync(WorkflowRunSnapshot run, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (mutationSync)
        {
            runs[run.RunId] = run;
        }

        return Task.CompletedTask;
    }

    public Task CreateRunWithStartedEventAsync(
        WorkflowRunSnapshot run,
        WorkflowEventRecord startedEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(startedEvent);
        cancellationToken.ThrowIfCancellationRequested();
        if (run.RunId != startedEvent.RunId)
        {
            throw new InvalidOperationException("Workflow run and started event must use the same run id.");
        }

        if (startedEvent.Kind != WorkflowEventKind.Started)
        {
            throw new InvalidOperationException("Initial workflow persistence requires a Started event.");
        }

        lock (mutationSync)
        {
            if (runs.ContainsKey(run.RunId))
            {
                throw new WorkflowRunAlreadyExistsException(run.RunId);
            }

            runs[run.RunId] = run;
            events.GetOrAdd(run.RunId, _ => new ConcurrentQueue<WorkflowEventRecord>())
                .Enqueue(startedEvent);
        }

        return Task.CompletedTask;
    }

    public Task<WorkflowRunTransitionResult> TryTransitionRunAsync(
        WorkflowRunId runId,
        IReadOnlyCollection<WorkflowRunState> expectedStates,
        WorkflowRunSnapshot updatedRun,
        WorkflowEventRecord? transitionEvent = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedStates);
        ArgumentNullException.ThrowIfNull(updatedRun);
        cancellationToken.ThrowIfCancellationRequested();
        if (updatedRun.RunId != runId || transitionEvent is not null && transitionEvent.RunId != runId)
        {
            throw new InvalidOperationException("Workflow transition records must use the requested run id.");
        }

        lock (mutationSync)
        {
            if (!runs.TryGetValue(runId, out var current) || !expectedStates.Contains(current.State))
            {
                return Task.FromResult(new WorkflowRunTransitionResult(false, current));
            }

            runs[runId] = updatedRun;
            if (transitionEvent is not null)
            {
                events.GetOrAdd(runId, _ => new ConcurrentQueue<WorkflowEventRecord>())
                    .Enqueue(transitionEvent);
            }

            return Task.FromResult(new WorkflowRunTransitionResult(true, updatedRun));
        }
    }

    public Task<WorkflowExternalResponseAcceptanceResult> TryAcceptExternalResponseAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        DateTimeOffset respondedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);
        cancellationToken.ThrowIfCancellationRequested();
        lock (mutationSync)
        {
            if (!requests.TryGetValue(requestId, out var request))
            {
                return Task.FromResult(new WorkflowExternalResponseAcceptanceResult(
                    WorkflowExternalResponseAcceptanceOutcome.NotFound,
                    Request: null));
            }

            if (request.RespondedAtUtc.HasValue)
            {
                return Task.FromResult(new WorkflowExternalResponseAcceptanceResult(
                    WorkflowExternalResponseAcceptanceOutcome.AlreadyResponded,
                    request));
            }

            var accepted = request with
            {
                ResponseJson = responseJson,
                RespondedAtUtc = respondedAtUtc
            };
            requests[requestId] = accepted;
            return Task.FromResult(new WorkflowExternalResponseAcceptanceResult(
                WorkflowExternalResponseAcceptanceOutcome.Accepted,
                accepted));
        }
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

        if (request.VersionId.HasValue)
        {
            filtered = filtered.Where(run => run.VersionId == request.VersionId.Value);
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

        var ordered = filtered.OrderByDescending(run => run.UpdatedAtUtc);
        if (!request.IncludeTotalCount)
        {
            var boundedItems = ordered
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToArray();
            return Task.FromResult(new WorkflowListPage<WorkflowRunSnapshot>(
                boundedItems,
                pageIndex,
                pageSize,
                boundedItems.Length));
        }

        var orderedItems = ordered.ToArray();
        var items = orderedItems
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult(new WorkflowListPage<WorkflowRunSnapshot>(
            items,
            pageIndex,
            pageSize,
            orderedItems.Length));
    }

    public Task<WorkflowOverviewStoreSnapshot> QueryOverviewAsync(
        WorkflowOverviewStoreQuery request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOverviewTake(request.RecentTake, MaximumOverviewRecentTake, nameof(request.RecentTake));
        ValidateOverviewTake(
            request.TopWorkflowTake,
            MaximumOverviewTopWorkflowTake,
            nameof(request.TopWorkflowTake));
        cancellationToken.ThrowIfCancellationRequested();

        WorkflowRunSnapshot[] snapshot;
        lock (mutationSync)
        {
            snapshot = runs.Values.ToArray();
        }

        var result = new WorkflowOverviewStoreSnapshot(
            snapshot
                .GroupBy(run => run.State)
                .ToDictionary(group => group.Key, group => group.Count()),
            snapshot
                .GroupBy(run => run.Backend)
                .ToDictionary(group => group.Key, group => group.Count()),
            snapshot
                .GroupBy(run => run.WorkflowId)
                .Select(group => new WorkflowOverviewStoreWorkflowRow(
                    group.Key,
                    group.Count(),
                    group.Count(run => run.State == WorkflowRunState.Failed),
                    group.Max(run => run.UpdatedAtUtc)))
                .OrderByDescending(row => row.RunCount)
                .ThenByDescending(row => row.LastRunAtUtc)
                .ThenBy(row => row.WorkflowId.Value)
                .Take(request.TopWorkflowTake)
                .ToArray(),
            snapshot
                .OrderByDescending(run => run.UpdatedAtUtc)
                .ThenByDescending(run => run.RunId.Value)
                .Take(request.RecentTake)
                .ToArray());
        return Task.FromResult(result);
    }

    public Task SaveEventAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (mutationSync)
        {
            events.GetOrAdd(workflowEvent.RunId, _ => new ConcurrentQueue<WorkflowEventRecord>())
                .Enqueue(workflowEvent);
        }

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
        cancellationToken.ThrowIfCancellationRequested();
        lock (mutationSync)
        {
            var isNew = !requests.ContainsKey(request.Id);
            requests[request.Id] = request;
            if (isNew)
            {
                requestsByRun.GetOrAdd(request.RunId, _ => new ConcurrentQueue<WorkflowExternalRequestId>())
                    .Enqueue(request.Id);
            }
        }

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
        cancellationToken.ThrowIfCancellationRequested();
        lock (mutationSync)
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
        lock (mutationSync)
        {
            var isNew = !requests.ContainsKey(request.Id);
            requests[request.Id] = request;
            if (isNew)
            {
                requestsByRun.GetOrAdd(request.RunId, _ => new ConcurrentQueue<WorkflowExternalRequestId>())
                    .Enqueue(request.Id);
            }
        }

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

    private static void ValidateOverviewTake(int value, int maximum, string parameterName)
    {
        if (value is < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Workflow overview take must be between 1 and {maximum}.");
        }
    }
}
