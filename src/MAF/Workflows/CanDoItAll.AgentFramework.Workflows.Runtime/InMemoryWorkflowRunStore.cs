using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowRunStore :
    IWorkflowRunStore,
    IWorkflowRedactedExternalResponseAcceptanceStore,
    IWorkflowArtifactStore,
    IWorkflowExternalRequestStore,
    IWorkflowOverviewStore,
    IWorkflowDashboardActivityStore
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
        return TryAcceptExternalResponseCore(
            requestId,
            responseJson,
            respondedAtUtc,
            cancellationToken);
    }

    Task<WorkflowExternalResponseAcceptanceResult>
        IWorkflowRedactedExternalResponseAcceptanceStore.TryAcceptRedactedExternalResponseAsync(
            WorkflowExternalRequestId requestId,
            DateTimeOffset respondedAtUtc,
            CancellationToken cancellationToken)
        => TryAcceptExternalResponseCore(
            requestId,
            string.Empty,
            respondedAtUtc,
            cancellationToken);

    private Task<WorkflowExternalResponseAcceptanceResult> TryAcceptExternalResponseCore(
        WorkflowExternalRequestId requestId,
        string responseJson,
        DateTimeOffset respondedAtUtc,
        CancellationToken cancellationToken)
    {
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

    internal bool TryApplyResumeCommit(
        InMemoryWorkflowRunCommitPlan plan,
        out WorkflowRunSnapshot? currentRun)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (mutationSync)
        {
            if (!runs.TryGetValue(plan.ExpectedRun.RunId, out currentRun) ||
                currentRun != plan.ExpectedRun ||
                !requests.TryGetValue(plan.ExpectedRequest.Id, out var currentRequest) ||
                currentRequest != plan.ExpectedRequest ||
                currentRequest.EffectiveState != WorkflowExternalRequestState.Pending ||
                currentRequest.RespondedAtUtc.HasValue ||
                plan.NextRequests.Select(request => request.Id).Distinct().Count() != plan.NextRequests.Count ||
                plan.NextRequests.Any(request => requests.ContainsKey(request.Id)) ||
                plan.Checkpoints.Select(checkpoint => checkpoint.Id).Distinct().Count() != plan.Checkpoints.Count ||
                plan.Checkpoints.Any(checkpoint => checkpoints.ContainsKey(checkpoint.Id)) ||
                HasArtifactConflict(plan) ||
                HasEventConflict(plan))
            {
                return false;
            }

            requests[plan.RespondedRequest.Id] = plan.RespondedRequest;
            foreach (var request in plan.NextRequests)
            {
                requests[request.Id] = request;
                requestsByRun.GetOrAdd(request.RunId, _ => new ConcurrentQueue<WorkflowExternalRequestId>())
                    .Enqueue(request.Id);
            }

            foreach (var checkpoint in plan.Checkpoints)
            {
                checkpoints[checkpoint.Id] = checkpoint;
                checkpointsByRun.GetOrAdd(checkpoint.RunId, _ => new ConcurrentQueue<WorkflowCheckpointId>())
                    .Enqueue(checkpoint.Id);
            }

            foreach (var artifact in plan.Artifacts)
            {
                artifacts.GetOrAdd(artifact.RunId, _ => new ConcurrentQueue<WorkflowArtifactRecord>())
                    .Enqueue(artifact);
            }

            var eventQueue = events.GetOrAdd(
                plan.UpdatedRun.RunId,
                _ => new ConcurrentQueue<WorkflowEventRecord>());
            foreach (var workflowEvent in plan.Events)
            {
                eventQueue.Enqueue(workflowEvent);
            }

            runs[plan.UpdatedRun.RunId] = plan.UpdatedRun;
            if (plan.TransitionEvent is not null)
            {
                eventQueue.Enqueue(plan.TransitionEvent);
            }

            currentRun = plan.UpdatedRun;
            return true;
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

    public Task<WorkflowDashboardActivityStoreResult> QueryActivityAsync(
        WorkflowDashboardActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        WorkflowRunSnapshot[] snapshot;
        lock (mutationSync)
        {
            snapshot = runs.Values.ToArray();
        }

        var activeRuns = OrderActivity(snapshot.Where(run => WorkflowRunActivityPolicy.IsActive(run.State)))
            .Take(query.Take)
            .Select(ToDashboardActivityRun)
            .ToArray();
        if (activeRuns.Length > 0)
        {
            return Task.FromResult(new WorkflowDashboardActivityStoreResult(
                WorkflowDashboardActivityMode.Active,
                activeRuns));
        }

        var recentRuns = OrderActivity(snapshot)
            .Take(query.Take)
            .Select(ToDashboardActivityRun)
            .ToArray();
        return Task.FromResult(new WorkflowDashboardActivityStoreResult(
            WorkflowDashboardActivityMode.RecentFallback,
            recentRuns));
    }

    private static IOrderedEnumerable<WorkflowRunSnapshot> OrderActivity(IEnumerable<WorkflowRunSnapshot> runs)
        => runs
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ThenByDescending(run => run.RunId.Value);

    private static WorkflowDashboardActivityRun ToDashboardActivityRun(WorkflowRunSnapshot run)
        => new(
            run.RunId,
            run.WorkflowId,
            run.State,
            run.Summary,
            run.UpdatedAtUtc);

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

    private bool HasArtifactConflict(InMemoryWorkflowRunCommitPlan plan)
    {
        var artifactIds = plan.Artifacts.Select(artifact => artifact.Id).ToArray();
        if (artifactIds.Distinct().Count() != artifactIds.Length)
        {
            return true;
        }

        return artifacts.TryGetValue(plan.UpdatedRun.RunId, out var storedArtifacts) &&
            storedArtifacts.Any(stored => artifactIds.Contains(stored.Id));
    }

    private bool HasEventConflict(InMemoryWorkflowRunCommitPlan plan)
    {
        var eventIds = plan.Events
            .Select(workflowEvent => workflowEvent.Id)
            .Concat(plan.TransitionEvent is { } transitionEvent ? [transitionEvent.Id] : [])
            .ToArray();
        if (eventIds.Distinct().Count() != eventIds.Length)
        {
            return true;
        }

        return events.TryGetValue(plan.UpdatedRun.RunId, out var storedEvents) &&
            storedEvents.Any(stored => eventIds.Contains(stored.Id));
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
