using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeProjectionQueryService(
    IProcessProjectionStore projectionStore,
    ProcessProjectionJsonCodec jsonCodec,
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore? runtimeStateStore = null,
    IProcessRuntimeStepAssignmentStore? assignmentStore = null,
    IProcessExecutionObservationReader? executionObservationReader = null)
{
    private const int LiveSnapshotReadLimit = 500;
    private static readonly TimeSpan ActiveExecutionStaleAfter = TimeSpan.FromMinutes(30);

    public async Task<ProcessLiveProcessesResult> GetLiveProcessesAsync(
        ProcessLiveProcessesQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateTake(query.Take, nameof(query.Take));
        if (query.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Window), query.Window, "Live process window must be positive.");
        }

        var nowUtc = query.NowUtc == default ? clock.GetUtcNow() : query.NowUtc;
        var windowStartUtc = nowUtc - query.Window;
        var snapshots = await projectionStore
            .ReadSnapshotsAsync(ProcessRuntimeProjectionProjector.ProjectorName, ProcessRuntimeProjectionKeys.LivePrefix, LiveSnapshotReadLimit, cancellationToken)
            .ConfigureAwait(false);
        var runs = new List<ProcessLiveProcessSnapshot>();

        foreach (var snapshot in snapshots)
        {
            var run = jsonCodec.ReadSnapshot<ProcessLiveProcessSnapshot>(snapshot);
            if (run.LastEventAtUtc < windowStartUtc)
            {
                continue;
            }

            runs.Add(run);
        }

        runs.Sort(static (left, right) =>
        {
            var lastEventComparison = right.LastEventAtUtc.CompareTo(left.LastEventAtUtc);
            return lastEventComparison != 0
                ? lastEventComparison
                : string.CompareOrdinal(left.RunId.ToString(), right.RunId.ToString());
        });

        if (runs.Count > query.Take)
        {
            runs.RemoveRange(query.Take, runs.Count - query.Take);
        }

        return new ProcessLiveProcessesResult(runs, CombineFreshness(runs));
    }

    public async Task<ProcessRunHistoryResult> GetRunHistoryAsync(
        ProcessRunHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateTake(query.Take, nameof(query.Take));
        if (query.Skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Skip), query.Skip, "Projection history skip count cannot be negative.");
        }

        if (query.FromUtc >= query.ToUtc)
        {
            throw new ArgumentException("History query range must have FromUtc earlier than ToUtc.", nameof(query));
        }

        var records = await projectionStore
            .ReadHistoryAsync(
                new ProcessProjectionHistoryQuery(
                    ProcessRuntimeProjectionProjector.ProjectorName,
                    query.RunId,
                    query.FromUtc,
                    query.ToUtc,
                    query.Take,
                    Skip: query.Skip),
                cancellationToken)
            .ConfigureAwait(false);
        var events = new List<ProcessTimelineEventProjection>(records.Count);

        foreach (var record in records)
        {
            events.Add(jsonCodec.ReadHistory<ProcessTimelineEventProjection>(record));
        }

        return new ProcessRunHistoryResult(events, CombineFreshness(events));
    }

    public async Task<ProcessRuntimeWorkspaceResult> GetRuntimeWorkspaceAsync(
        ProcessRuntimeWorkspaceQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateTake(query.TakeRuns, nameof(query.TakeRuns));
        ValidateTake(query.EventPageSize, nameof(query.EventPageSize));
        if (query.EventPage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.EventPage), query.EventPage, "Runtime event page cannot be negative.");
        }

        if (query.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Window), query.Window, "Runtime workspace window must be positive.");
        }

        var nowUtc = query.NowUtc == default ? clock.GetUtcNow() : query.NowUtc;
        var liveProcesses = await GetLiveProcessesAsync(
            new ProcessLiveProcessesQuery(nowUtc, query.Window, query.TakeRuns),
            cancellationToken).ConfigureAwait(false);
        var selectedRunId = ResolveSelectedRunId(liveProcesses.Runs, query.SelectedRunId);
        ProcessRunDetailProjection? selectedRun = null;
        if (selectedRunId is not null)
        {
            selectedRun = await GetRunDetailAsync(new ProcessRunDetailQuery(selectedRunId.Value), cancellationToken)
                .ConfigureAwait(false);
        }

        var history = await GetRunHistoryAsync(
            new ProcessRunHistoryQuery(
                selectedRunId,
                nowUtc - query.Window,
                nowUtc,
                Take: query.EventPageSize + 1,
                Skip: checked(query.EventPage * query.EventPageSize)),
            cancellationToken).ConfigureAwait(false);
        var events = history.Events.Take(query.EventPageSize).ToArray();
        var activeAgents = await LoadActiveAgentsAsync(liveProcesses.Runs, nowUtc, cancellationToken).ConfigureAwait(false);
        var freshness = CombineFreshness(liveProcesses.Freshness, history.Freshness, selectedRun?.Freshness);

        return new ProcessRuntimeWorkspaceResult(
            liveProcesses.Runs,
            selectedRun,
            events,
            history.Events.Count > query.EventPageSize,
            activeAgents,
            freshness);
    }

    public async Task<ProcessRunDetailProjection?> GetRunDetailAsync(
        ProcessRunDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var snapshot = await projectionStore
            .LoadSnapshotAsync(
                ProcessRuntimeProjectionProjector.ProjectorName,
                ProcessRuntimeProjectionKeys.RunDetail(query.RunId),
                cancellationToken)
            .ConfigureAwait(false);

        return snapshot is null
            ? null
            : jsonCodec.ReadSnapshot<ProcessRunDetailProjection>(snapshot);
    }

    private static void ValidateTake(int take, string parameterName)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, take, "Projection query size must be positive.");
        }
    }

    private static ProcessRunId? ResolveSelectedRunId(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        ProcessRunId? requestedRunId)
    {
        if (requestedRunId is not null && runs.Any(run => run.RunId == requestedRunId))
        {
            return requestedRunId;
        }

        return runs
            .OrderByDescending(run => run.Status == ProcessProjectedRunStatus.NeedsAttention)
            .ThenByDescending(run => run.IsActive)
            .ThenByDescending(run => run.LastEventAtUtc)
            .Select(run => (ProcessRunId?)run.RunId)
            .FirstOrDefault();
    }

    private static ProcessProjectionFreshness? CombineFreshness(IReadOnlyList<ProcessLiveProcessSnapshot> runs)
    {
        if (runs.Count == 0)
        {
            return null;
        }

        var latest = runs[0].Freshness;
        for (var index = 1; index < runs.Count; index++)
        {
            if (runs[index].Freshness.SourceGlobalSequence > latest.SourceGlobalSequence)
            {
                latest = runs[index].Freshness;
            }
        }

        return latest;
    }

    private static ProcessProjectionFreshness? CombineFreshness(IReadOnlyList<ProcessTimelineEventProjection> events)
    {
        if (events.Count == 0)
        {
            return null;
        }

        var latestEvent = events[^1];
        return new ProcessProjectionFreshness(
            latestEvent.OccurredAtUtc,
            latestEvent.GlobalSequence,
            new ProcessProjectionLag(latestEvent.GlobalSequence, latestEvent.GlobalSequence, 0));
    }

    private static ProcessProjectionFreshness? CombineFreshness(params ProcessProjectionFreshness?[] freshnessValues)
    {
        ProcessProjectionFreshness? latest = null;
        foreach (var freshness in freshnessValues)
        {
            if (freshness is null)
            {
                continue;
            }

            if (latest is null || freshness.SourceGlobalSequence > latest.SourceGlobalSequence)
            {
                latest = freshness;
            }
        }

        return latest;
    }

    private async Task<IReadOnlyList<ProcessRuntimeActiveAgentProjection>> LoadActiveAgentsAsync(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (runtimeStateStore is null || assignmentStore is null || runs.Count == 0)
        {
            return [];
        }

        var activeAgents = new List<ProcessRuntimeActiveAgentProjection>();
        var observedStepKeys = new HashSet<(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId)>();
        var assignmentsByRun = new Dictionary<ProcessRunId, IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment>>();
        var runById = runs.ToDictionary(run => run.RunId);

        foreach (var run in runs)
        {
            var assignments = await assignmentStore.LoadByRunAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            assignmentsByRun[run.RunId] = assignments
                .GroupBy(assignment => assignment.StepInstanceId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        if (executionObservationReader is not null)
        {
            var windowStartUtc = runs.Count == 0
                ? nowUtc - ActiveExecutionStaleAfter
                : runs.Min(run => run.FirstEventAtUtc).AddMinutes(-5);
            var observations = await executionObservationReader.ListAsync(
                new ProcessExecutionObservationQuery(
                    runs.Select(run => run.RunId).Distinct().ToArray(),
                    windowStartUtc,
                    nowUtc,
                    TakePerRun: 25),
                cancellationToken).ConfigureAwait(false);

            foreach (var observation in observations
                         .Where(observation => runById.ContainsKey(observation.RunId))
                         .OrderByDescending(observation => observation.UpdatedAtUtc))
            {
                assignmentsByRun.TryGetValue(observation.RunId, out var assignmentsByStep);
                ProcessRuntimeStepAssignment? assignment = null;
                assignmentsByStep?.TryGetValue(observation.StepInstanceId, out assignment);
                var run = runById[observation.RunId];
                var isTerminal = IsExecutionTerminal(observation.State);
                var isStale = !isTerminal && observation.UpdatedAtUtc < nowUtc - ActiveExecutionStaleAfter;
                if (isTerminal || isStale)
                {
                    continue;
                }

                observedStepKeys.Add((observation.RunId, observation.StepInstanceId));
                activeAgents.Add(CreateObservedAgentProjection(
                    run,
                    assignment,
                    observation,
                    isWorking: !isTerminal && !isStale,
                    isStale));
            }
        }

        foreach (var run in runs)
        {
            var state = await runtimeStateStore.LoadAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                continue;
            }

            if (!assignmentsByRun.TryGetValue(run.RunId, out var assignmentsByStep))
            {
                continue;
            }

            foreach (var step in state.Steps.Where(IsAgentActiveStep))
            {
                if (observedStepKeys.Contains((run.RunId, step.StepInstanceId)))
                {
                    continue;
                }

                if (!assignmentsByStep.TryGetValue(step.StepInstanceId, out var assignment))
                {
                    continue;
                }

                if (executionObservationReader is not null)
                {
                    continue;
                }

                var claim = step.ActiveClaimToken is { } activeClaimToken
                    ? state.Claims.FirstOrDefault(item => item.ClaimToken == activeClaimToken)
                    : null;
                var isLeaseExpired = claim is not null && claim.ExpiresAtUtc < nowUtc;
                var isWorking = !isLeaseExpired &&
                    step.Status is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running;
                activeAgents.Add(new ProcessRuntimeActiveAgentProjection(
                    run.RunId.Value,
                    step.StepInstanceId.Value,
                    BuildRunLabel(run),
                    assignment.StepKey,
                    assignment.RoleKey,
                    assignment.ExecutorKind,
                    assignment.ExecutorId,
                    assignment.ExecutorDisplayName,
                    step.Status.ToString(),
                    isWorking,
                    isLeaseExpired,
                    state.UpdatedAtUtc,
                    claim?.CreatedAtUtc,
                    claim?.ExpiresAtUtc,
                    BuildActiveAgentSummary(assignment, step, claim, isLeaseExpired, executionObservationReader is not null))
                {
                    AgentId = Guid.TryParse(assignment.ExecutorId, out var parsedAgentId) ? parsedAgentId : null,
                    AgentName = assignment.ExecutorDisplayName,
                    ObservationSource = executionObservationReader is null
                        ? "Runtime claim"
                        : "Runtime claim without AgentFramework execution evidence",
                    CurrentActivity = executionObservationReader is null
                        ? $"{assignment.StepKey} is {step.Status}."
                        : $"No AgentFramework execution run was observed for step {assignment.StepKey}."
                });
            }
        }

        return activeAgents
            .OrderByDescending(agent => agent.IsWorking)
            .ThenByDescending(agent => string.Equals(agent.Status, nameof(ProcessRuntimeStepStatus.Running), StringComparison.Ordinal))
            .ThenByDescending(agent => agent.UpdatedAtUtc)
            .ThenBy(agent => agent.ExecutorDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsAgentActiveStep(ProcessRuntimeStepState step)
        => step.Status is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running;

    private static string BuildRunLabel(ProcessLiveProcessSnapshot run)
        => $"Run {run.RunId.Value.ToString("N")[..8]}";

    private static string BuildActiveAgentSummary(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeStepState step,
        DispatchClaimState? claim,
        bool isLeaseExpired,
        bool executionObservationExpected)
    {
        var claimSummary = claim is null
            ? "No active dispatch lease is attached."
            : isLeaseExpired
                ? $"Lease expired {claim.ExpiresAtUtc.LocalDateTime:g}."
                : $"Lease expires {claim.ExpiresAtUtc.LocalDateTime:g}.";
        var evidenceSummary = executionObservationExpected
            ? " AgentFramework execution-run evidence is missing for this active claim."
            : string.Empty;
        return $"{assignment.ExecutorDisplayName} is {step.Status} on {assignment.StepKey} as {assignment.RoleKey}. {claimSummary}{evidenceSummary}";
    }

    private static ProcessRuntimeActiveAgentProjection CreateObservedAgentProjection(
        ProcessLiveProcessSnapshot run,
        ProcessRuntimeStepAssignment? assignment,
        ProcessExecutionObservation observation,
        bool isWorking,
        bool isStale)
    {
        var stepKey = string.IsNullOrWhiteSpace(assignment?.StepKey)
            ? observation.StepInstanceId.ToString()
            : assignment.StepKey;
        var roleKey = string.IsNullOrWhiteSpace(assignment?.RoleKey)
            ? "agent"
            : assignment.RoleKey;
        var displayName = FirstNonEmpty(observation.AgentName, assignment?.ExecutorDisplayName, observation.AgentId.ToString("D"));
        var currentActivity = ResolveCurrentActivity(observation);

        return new ProcessRuntimeActiveAgentProjection(
            run.RunId.Value,
            observation.StepInstanceId.Value,
            BuildRunLabel(run),
            stepKey,
            roleKey,
            ProcessLaunchExecutorKinds.Agent,
            observation.AgentId.ToString("D"),
            displayName,
            observation.State,
            isWorking,
            isStale,
            observation.UpdatedAtUtc,
            observation.StartedAtUtc ?? observation.CreatedAtUtc,
            LeaseExpiresAtUtc: null,
            BuildObservedAgentSummary(displayName, stepKey, roleKey, observation, isStale, currentActivity))
        {
            ExecutionRunId = observation.ExecutionRunId,
            AgentId = observation.AgentId,
            AgentName = displayName,
            ProviderName = observation.ProviderName,
            Model = observation.Model,
            ExecutionState = observation.State,
            ExecutionOutcome = observation.Outcome,
            ExecutionStartedAtUtc = observation.StartedAtUtc ?? observation.CreatedAtUtc,
            ExecutionUpdatedAtUtc = observation.UpdatedAtUtc,
            CurrentActivity = currentActivity,
            LastError = observation.LastError,
            ObservationSource = "AgentFramework execution run",
            RecentActivities = observation.RecentActivities
                .Select(activity => new ProcessRuntimeActiveAgentActivityProjection(
                    activity.CreatedAtUtc,
                    activity.State,
                    activity.Phase,
                    activity.Message))
                .ToArray(),
            RecentTools = observation.RecentTools
                .Select(tool => new ProcessRuntimeActiveAgentToolProjection(
                    tool.ToolName,
                    tool.RuntimeToolProviderKey,
                    tool.RequestSummary,
                    tool.ExitSummary,
                    tool.StartedAtUtc,
                    tool.CompletedAtUtc))
                .ToArray(),
            Artifacts = observation.Artifacts
                .Select(artifact => new ProcessRuntimeActiveAgentArtifactProjection(
                    artifact.ArtifactKind,
                    artifact.DisplayName,
                    artifact.RelativePath,
                    artifact.Summary,
                    artifact.CreatedAtUtc))
                .ToArray()
        };
    }

    private static string ResolveCurrentActivity(ProcessExecutionObservation observation)
    {
        var latestActivity = observation.RecentActivities
            .OrderByDescending(activity => activity.CreatedAtUtc)
            .FirstOrDefault();
        if (latestActivity is not null && !string.IsNullOrWhiteSpace(latestActivity.Message))
        {
            return $"{latestActivity.Phase}: {latestActivity.Message}";
        }

        return FirstNonEmpty(
            observation.ResultSummary,
            observation.InputSummary,
            $"{observation.State} execution run {observation.ExecutionRunId:D}.");
    }

    private static string BuildObservedAgentSummary(
        string displayName,
        string stepKey,
        string roleKey,
        ProcessExecutionObservation observation,
        bool isStale,
        string currentActivity)
    {
        var staleText = isStale ? " Signal is stale." : string.Empty;
        var outcomeText = string.IsNullOrWhiteSpace(observation.Outcome) ? string.Empty : $" Outcome: {observation.Outcome}.";
        return $"{displayName} is {observation.State} on {stepKey} as {roleKey}.{outcomeText} {currentActivity}{staleText}".Trim();
    }

    private static bool IsExecutionTerminal(string state)
        => string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
