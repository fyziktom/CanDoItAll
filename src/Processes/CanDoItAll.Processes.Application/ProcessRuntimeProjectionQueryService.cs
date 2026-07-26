using System.Globalization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeProjectionQueryService(
    IProcessProjectionStore projectionStore,
    ProcessProjectionJsonCodec jsonCodec,
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore? runtimeStateStore = null,
    IProcessRuntimeStepAssignmentStore? assignmentStore = null,
    IProcessExecutionObservationReader? executionObservationReader = null,
    IProcessRunRecordStore? runRecordStore = null)
{
    private const int LiveSnapshotReadLimit = 500;
    private const int RuntimeMetricEventReadLimit = 10_000;
    private const int OperatorActionObservationTakePerRun = 100;
    private const string MissingBlockedDiagnosticCode = "process.runtime.blocked_without_diagnostics";
    private const string MissingBlockedDiagnosticSummary =
        "Step blocked without strategy diagnostics. Inspect the result receipt, assignment, and execution observation for the missing cause.";
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

        var loadOptions = query.LoadOptions ?? ProcessLiveProcessesLoadOptions.Full;
        var enrichmentCache = new RuntimeRunEnrichmentCache(runtimeStateStore, assignmentStore);
        if (loadOptions.IncludeAttentionReconciliation)
        {
            runs = (await ReconcileLiveActivityAsync(runs, nowUtc, enrichmentCache, cancellationToken).ConfigureAwait(false)).ToList();
        }

        if (loadOptions.IncludeOperatorActions)
        {
            runs = (await EnrichOperatorActionsAsync(runs, nowUtc, enrichmentCache, cancellationToken).ConfigureAwait(false)).ToList();
        }

        if (loadOptions.IncludeCurrentSteps)
        {
            runs = (await EnrichCurrentStepsAsync(runs, nowUtc, enrichmentCache, cancellationToken).ConfigureAwait(false)).ToList();
        }

        if (loadOptions.IncludeChildRunWaits)
        {
            runs = (await EnrichChildRunWaitsAsync(runs, enrichmentCache, cancellationToken).ConfigureAwait(false)).ToList();
        }

        if (loadOptions.IncludeDiagnostics)
        {
            runs = (await EnrichLiveDiagnosticsAsync(runs, enrichmentCache, cancellationToken).ConfigureAwait(false)).ToList();
        }

        if (enrichmentCache.CanLoadAssignments &&
            (loadOptions.IncludeCurrentSteps || loadOptions.IncludeOperatorActions || loadOptions.IncludeChildRunWaits))
        {
            runs = (await EnrichRunMetadataAsync(runs, enrichmentCache, cancellationToken).ConfigureAwait(false)).ToList();
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

        var enrichedEvents = await EnrichTimelineDiagnosticsAsync(
            query.RunId,
            events,
            cancellationToken).ConfigureAwait(false);

        return new ProcessRunHistoryResult(enrichedEvents, CombineFreshness(enrichedEvents));
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
        var loadOptions = query.LoadOptions ?? ProcessRuntimeWorkspaceLoadOptions.Full;
        var liveProcesses = await GetLiveProcessesAsync(
            new ProcessLiveProcessesQuery(nowUtc, query.Window, query.TakeRuns, loadOptions.LiveProcesses),
            cancellationToken).ConfigureAwait(false);
        var selectedRunId = RequiresSelectedRunId(loadOptions)
            ? ResolveSelectedRunId(liveProcesses.Runs, query.SelectedRunId, query.AutoSelectRun)
            : query.SelectedRunId;
        var selectedRunSnapshot = selectedRunId is { } resolvedSelectedRunId
            ? liveProcesses.Runs.FirstOrDefault(run => run.RunId == resolvedSelectedRunId)
            : null;
        var selectedRunNeedsRecordLookup = selectedRunId is not null &&
            (selectedRunSnapshot is null ||
             selectedRunSnapshot.Status is
            ProcessProjectedRunStatus.Completed or
            ProcessProjectedRunStatus.Failed or
            ProcessProjectedRunStatus.Cancelled);
        if (loadOptions.IncludeRunRecord &&
            selectedRunNeedsRecordLookup &&
            runRecordStore is null)
        {
            throw new InvalidOperationException(
                "Process run records are required by the runtime workspace query but no record store is registered.");
        }

        var selectedRunRecord = !loadOptions.IncludeRunRecord ||
            !selectedRunNeedsRecordLookup
            ? null
            : await runRecordStore!
                .GetAsync(selectedRunId.Value, includeSuperseded: false, cancellationToken)
                .ConfigureAwait(false);
        var hasTerminalRecord = selectedRunRecord is not null;
        ProcessRunDetailProjection? selectedRun = null;
        if (loadOptions.IncludeSelectedRun && selectedRunId is not null)
        {
            selectedRun = await GetRunDetailAsync(new ProcessRunDetailQuery(selectedRunId.Value), cancellationToken)
                .ConfigureAwait(false);
        }

        var history = loadOptions.IncludeHistory
            ? await GetRunHistoryAsync(
                new ProcessRunHistoryQuery(
                    selectedRunId,
                    nowUtc - query.Window,
                    nowUtc,
                    Take: query.EventPageSize + 1,
                    Skip: checked(query.EventPage * query.EventPageSize)),
                cancellationToken).ConfigureAwait(false)
            : new ProcessRunHistoryResult([], Freshness: null);
        var metricHistory = loadOptions.IncludeMetricHistory && !hasTerminalRecord
            ? await GetRunHistoryAsync(
                new ProcessRunHistoryQuery(
                    selectedRunId,
                    nowUtc - query.Window,
                    nowUtc,
                    Take: RuntimeMetricEventReadLimit),
                cancellationToken).ConfigureAwait(false)
            : new ProcessRunHistoryResult([], Freshness: null);
        var events = history.Events.Take(query.EventPageSize).ToArray();
        var activeAgents = loadOptions.IncludeActiveAgents
            ? await LoadActiveAgentsAsync(liveProcesses.Runs, nowUtc, cancellationToken).ConfigureAwait(false)
            : [];
        var freshness = CombineFreshness(liveProcesses.Freshness, history.Freshness, metricHistory.Freshness, selectedRun?.Freshness);

        return new ProcessRuntimeWorkspaceResult(
            liveProcesses.Runs,
            selectedRun,
            events,
            metricHistory.Events,
            history.Events.Count > query.EventPageSize,
            activeAgents,
            freshness)
        {
            SelectedRunRecord = selectedRunRecord
        };
    }

    private static bool RequiresSelectedRunId(ProcessRuntimeWorkspaceLoadOptions loadOptions)
        => loadOptions.IncludeSelectedRun ||
            loadOptions.IncludeRunRecord ||
            loadOptions.IncludeHistory ||
            loadOptions.IncludeMetricHistory ||
            loadOptions.IncludeActiveAgents;

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

        if (snapshot is null)
        {
            return null;
        }

        var detail = jsonCodec.ReadSnapshot<ProcessRunDetailProjection>(snapshot);
        return await EnrichRunDetailDiagnosticsAsync(detail, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessRunDetailProjection> EnrichRunDetailDiagnosticsAsync(
        ProcessRunDetailProjection detail,
        CancellationToken cancellationToken)
    {
        var enrichmentCache = new RuntimeRunEnrichmentCache(runtimeStateStore, assignmentStore);
        var enrichment = await BuildRunDiagnosticEnrichmentAsync(
            detail.RunId,
            enrichmentCache,
            cancellationToken).ConfigureAwait(false);
        if (enrichment.Lineage.Count == 0)
        {
            return detail;
        }

        return detail with
        {
            Diagnostics = enrichment.CurrentDiagnostics,
            ResultLineage = enrichment.Lineage,
            RecentEvents = EnrichLiveEvents(detail.RecentEvents, enrichment.CurrentDiagnostics)
        };
    }

    private async Task<IReadOnlyList<ProcessTimelineEventProjection>> EnrichTimelineDiagnosticsAsync(
        ProcessRunId? runId,
        IReadOnlyList<ProcessTimelineEventProjection> events,
        CancellationToken cancellationToken)
    {
        if (runId is null || events.Count == 0)
        {
            return events;
        }

        var enrichmentCache = new RuntimeRunEnrichmentCache(runtimeStateStore, assignmentStore);
        var diagnostics = await BuildRunDiagnosticsAsync(
            runId.Value,
            enrichmentCache,
            cancellationToken).ConfigureAwait(false);
        if (diagnostics.Count == 0)
        {
            return events;
        }

        return events
            .Select(runtimeEvent => EnrichTimelineEvent(runtimeEvent, diagnostics))
            .ToArray();
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
        ProcessRunId? requestedRunId,
        bool autoSelectRun)
    {
        if (requestedRunId is not null)
        {
            return requestedRunId;
        }

        if (!autoSelectRun)
        {
            return null;
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

    private async Task<IReadOnlyList<ProcessRuntimeDiagnosticProjection>> BuildRunDiagnosticsAsync(
        ProcessRunId runId,
        RuntimeRunEnrichmentCache enrichmentCache,
        CancellationToken cancellationToken)
    {
        var enrichment = await BuildRunDiagnosticEnrichmentAsync(runId, enrichmentCache, cancellationToken).ConfigureAwait(false);
        return enrichment.CurrentDiagnostics;
    }

    private async Task<IReadOnlyList<ProcessRuntimeResultLineageProjection>> BuildResultLineageAsync(
        ProcessRunId runId,
        RuntimeRunEnrichmentCache enrichmentCache,
        CancellationToken cancellationToken)
    {
        if (!enrichmentCache.CanLoadStates)
        {
            return [];
        }

        var state = await enrichmentCache.LoadStateAsync(runId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return [];
        }

        var assignmentsByStep = await enrichmentCache
            .LoadAssignmentsByStepAsync(runId, cancellationToken)
            .ConfigureAwait(false);

        return BuildResultLineage(state, assignmentsByStep);
    }

    private async Task<ProcessRunDiagnosticEnrichment> BuildRunDiagnosticEnrichmentAsync(
        ProcessRunId runId,
        RuntimeRunEnrichmentCache enrichmentCache,
        CancellationToken cancellationToken)
    {
        if (!enrichmentCache.CanLoadStates)
        {
            return new ProcessRunDiagnosticEnrichment([], []);
        }

        var state = await enrichmentCache.LoadStateAsync(runId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return new ProcessRunDiagnosticEnrichment([], []);
        }

        var assignmentsByStep = await enrichmentCache
            .LoadAssignmentsByStepAsync(runId, cancellationToken)
            .ConfigureAwait(false);
        var lineage = BuildResultLineage(state, assignmentsByStep);
        return new ProcessRunDiagnosticEnrichment(
            lineage,
            BuildCurrentDiagnostics(state, lineage));
    }

    private static IReadOnlyList<ProcessRuntimeResultLineageProjection> BuildResultLineage(
        ProcessRuntimeStateSnapshot state,
        IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment> assignmentsByStep)
    {
        if (state.AppliedResults.Count == 0)
        {
            return [];
        }

        var lineage = new List<ProcessRuntimeResultLineageProjection>(state.AppliedResults.Count);
        foreach (var receipt in state.AppliedResults)
        {
            var stepKey = ResolveStepKey(receipt.StepInstanceId, assignmentsByStep);
            var diagnostics = BuildDiagnostics(state.RunId, receipt, stepKey);
            lineage.Add(new ProcessRuntimeResultLineageProjection(
                state.RunId.Value,
                receipt.StepInstanceId.Value,
                stepKey,
                receipt.StrategyId.Value,
                receipt.IdempotencyKey.Value,
                receipt.Outcome.ToString(),
                receipt.AppliedStepStatus.ToString(),
                receipt.ResultHash,
                diagnostics,
                receipt.ProducedArtifacts
                    .Select(artifact => new ProcessRuntimeArtifactLineageProjection(
                        artifact.SlotId.Value,
                        artifact.ArtifactId.Value,
                        artifact.ContentHash))
                    .ToArray(),
                receipt.RecoveryDecision is null
                    ? null
                    : new ProcessRuntimeRecoveryDecisionProjection(
                        receipt.RecoveryDecision.FailureCategory.ToString(),
                        receipt.RecoveryDecision.DecisionKind.ToString(),
                        receipt.RecoveryDecision.SourceDiagnosticCode,
                        receipt.RecoveryDecision.Policy,
                        receipt.RecoveryDecision.SafeReason)));
        }

        return lineage;
    }

    private static IReadOnlyList<ProcessRuntimeDiagnosticProjection> BuildCurrentDiagnostics(
        ProcessRuntimeStateSnapshot state,
        IReadOnlyList<ProcessRuntimeResultLineageProjection> lineage)
    {
        if (lineage.Count == 0)
        {
            return [];
        }

        var currentProblemSteps = state.Steps
            .Where(step => step.IsExecutable &&
                           step.Status is ProcessRuntimeStepStatus.Blocked or ProcessRuntimeStepStatus.Failed)
            .ToDictionary(step => step.StepInstanceId.Value, step => step.Status.ToString());
        if (currentProblemSteps.Count == 0)
        {
            return [];
        }

        return lineage
            .Where(item =>
                currentProblemSteps.TryGetValue(item.StepInstanceId, out var stepStatus) &&
                string.Equals(item.AppliedStepStatus, stepStatus, StringComparison.Ordinal))
            .GroupBy(item => item.StepInstanceId)
            .Select(group => group.Last())
            .SelectMany(item => item.Diagnostics)
            .ToArray();
    }

    private static IReadOnlyList<ProcessRuntimeDiagnosticProjection> BuildCurrentStepDiagnostics(
        ProcessRuntimeStepState currentStep,
        IReadOnlyList<ProcessRuntimeResultLineageProjection> stepLineage)
    {
        if (stepLineage.Count == 0)
        {
            return [];
        }

        var currentStatus = currentStep.Status.ToString();
        var currentStatusReceipt = stepLineage
            .LastOrDefault(item => string.Equals(item.AppliedStepStatus, currentStatus, StringComparison.Ordinal));
        if (currentStatusReceipt is not null)
        {
            return currentStatusReceipt.Diagnostics;
        }

        if (currentStep.Status == ProcessRuntimeStepStatus.Ready)
        {
            var safeRetryReceipt = stepLineage.LastOrDefault(item =>
                item.RecoveryDecision is
                {
                    DecisionKind: nameof(ProcessRecoveryDecisionKind.SafeRetry),
                    Policy: "process.current-step-safe-retry"
                });
            return safeRetryReceipt?.Diagnostics ?? [];
        }

        return [];
    }

    private static IReadOnlyList<ProcessRuntimeDiagnosticProjection> BuildDiagnostics(
        ProcessRunId runId,
        StrategyResultReceipt receipt,
        string stepKey)
    {
        if (receipt.Diagnostics.Count == 0 &&
            receipt.AppliedStepStatus is ProcessRuntimeStepStatus.Blocked or ProcessRuntimeStepStatus.Failed)
        {
            return
            [
                new ProcessRuntimeDiagnosticProjection(
                    runId.Value,
                    receipt.StepInstanceId.Value,
                    stepKey,
                    receipt.StrategyId.Value,
                    receipt.ResultHash,
                    MissingBlockedDiagnosticCode,
                    ProcessRuntimeDiagnosticCategories.MissingDiagnostics,
                    MissingBlockedDiagnosticSummary,
                    nameof(ProcessProjectedSensitivity.Normal),
                    nameof(ProcessDiagnosticRetrySafety.Unknown),
                    nameof(ProcessDiagnosticIdempotencyClassification.Unknown),
                    RestrictedDiagnosticReference: null)
            ];
        }

        return receipt.Diagnostics
            .Select(diagnostic => new ProcessRuntimeDiagnosticProjection(
                    runId.Value,
                    receipt.StepInstanceId.Value,
                    stepKey,
                    receipt.StrategyId.Value,
                    receipt.ResultHash,
                    diagnostic.Code,
                    ClassifyDiagnosticCode(diagnostic.Code),
                    diagnostic.SafeSummary,
                    diagnostic.Sensitivity.ToString(),
                    diagnostic.RetrySafety.ToString(),
                    diagnostic.Idempotency.ToString(),
                    diagnostic.RestrictedEvidenceReference)
                {
                    OperatorDetails = ProcessRuntimeOperatorDiagnosticDetailsBuilder.Create(
                        diagnostic.Code,
                        diagnostic.SafeSummary)
                })
            .ToArray();
    }

    private static IReadOnlyList<ProcessLiveRunEventProjection> EnrichLiveEvents(
        IReadOnlyList<ProcessLiveRunEventProjection> events,
        IReadOnlyList<ProcessRuntimeDiagnosticProjection> diagnostics)
    {
        if (events.Count == 0 || diagnostics.Count == 0)
        {
            return events;
        }

        return events
            .Select(runtimeEvent => EnrichLiveEvent(runtimeEvent, diagnostics))
            .ToArray();
    }

    private static ProcessLiveRunEventProjection EnrichLiveEvent(
        ProcessLiveRunEventProjection runtimeEvent,
        IReadOnlyList<ProcessRuntimeDiagnosticProjection> diagnostics)
    {
        if (!IsAttentionEvent(runtimeEvent.EventType))
        {
            return runtimeEvent;
        }

        return runtimeEvent with
        {
            Diagnostics = diagnostics,
            Summary = BuildDiagnosticEventSummary(runtimeEvent.Summary, diagnostics)
        };
    }

    private static ProcessTimelineEventProjection EnrichTimelineEvent(
        ProcessTimelineEventProjection runtimeEvent,
        IReadOnlyList<ProcessRuntimeDiagnosticProjection> diagnostics)
    {
        if (!IsAttentionEvent(runtimeEvent.EventType))
        {
            return runtimeEvent;
        }

        return runtimeEvent with
        {
            Diagnostics = diagnostics,
            Summary = BuildDiagnosticEventSummary(runtimeEvent.Summary, diagnostics)
        };
    }

    private static bool IsAttentionEvent(string eventType)
        => eventType.EndsWith("Blocked", StringComparison.Ordinal) ||
           eventType.EndsWith("Failed", StringComparison.Ordinal) ||
           string.Equals(eventType, ProcessRuntimeProjectionEventTypeNames.ManagerIncidentRaised, StringComparison.Ordinal) ||
           string.Equals(eventType, ProcessRuntimeProjectionEventTypeNames.ManagerLoopBudgetEscalated, StringComparison.Ordinal) ||
           string.Equals(eventType, ProcessRuntimeProjectionEventTypeNames.ManagerRecoveryDenied, StringComparison.Ordinal) ||
           string.Equals(eventType, ProcessRuntimeProjectionEventTypeNames.ManagerBranchDecisionRejected, StringComparison.Ordinal);

    private static string BuildDiagnosticEventSummary(
        string summary,
        IReadOnlyList<ProcessRuntimeDiagnosticProjection> diagnostics)
    {
        var primary = diagnostics[0];
        return $"{summary}: {primary.Category} - {primary.SafeSummary}";
    }

    private static string ClassifyDiagnosticCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return ProcessRuntimeDiagnosticCategories.Unknown;
        }

        if (code.StartsWith("process.runtime.", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessRuntimeDiagnosticCategories.Runtime;
        }

        if (code.StartsWith("process.adapter.", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessRuntimeDiagnosticCategories.Adapter;
        }

        if (code.Contains("capability", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("tool", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("mcp", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("skill", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessRuntimeDiagnosticCategories.Capability;
        }

        if (code.Contains("artifact", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("receipt", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessRuntimeDiagnosticCategories.Artifact;
        }

        if (code.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("provider", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessRuntimeDiagnosticCategories.Provider;
        }

        return ProcessRuntimeDiagnosticCategories.Strategy;
    }

    private static string ResolveStepKey(
        ProcessStepInstanceId stepInstanceId,
        IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment> assignmentsByStep)
    {
        return assignmentsByStep.TryGetValue(stepInstanceId, out var assignment) &&
               !string.IsNullOrWhiteSpace(assignment.StepKey)
            ? assignment.StepKey
            : stepInstanceId.Value.ToString("D");
    }

    private static class ProcessRuntimeDiagnosticCategories
    {
        public const string Adapter = "Adapter";
        public const string Artifact = "Artifact";
        public const string Capability = "Capability";
        public const string MissingDiagnostics = "MissingDiagnostics";
        public const string Provider = "Provider";
        public const string Runtime = "Runtime";
        public const string Strategy = "Strategy";
        public const string Unknown = "Unknown";
    }

    private sealed record ProcessRunDiagnosticEnrichment(
        IReadOnlyList<ProcessRuntimeResultLineageProjection> Lineage,
        IReadOnlyList<ProcessRuntimeDiagnosticProjection> CurrentDiagnostics);

    private sealed class RuntimeRunEnrichmentCache(
        IProcessRuntimeStateStore? runtimeStateStore,
        IProcessRuntimeStepAssignmentStore? assignmentStore)
    {
        private readonly Dictionary<ProcessRunId, ProcessRuntimeStateSnapshot?> stateByRunId = [];
        private readonly Dictionary<ProcessRunId, IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment>> assignmentsByRunId = [];

        public bool CanLoadStates => runtimeStateStore is not null;

        public bool CanLoadAssignments => assignmentStore is not null;

        public async ValueTask<ProcessRuntimeStateSnapshot?> LoadStateAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken)
        {
            if (runtimeStateStore is null)
            {
                return null;
            }

            if (stateByRunId.TryGetValue(runId, out var cached))
            {
                return cached;
            }

            var loaded = await runtimeStateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
            stateByRunId[runId] = loaded;
            return loaded;
        }

        public async ValueTask<IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment>> LoadAssignmentsByStepAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken)
        {
            if (assignmentStore is null)
            {
                return new Dictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment>();
            }

            if (assignmentsByRunId.TryGetValue(runId, out var cached))
            {
                return cached;
            }

            var assignments = await assignmentStore.LoadByRunAsync(runId, cancellationToken).ConfigureAwait(false);
            var loaded = assignments
                .GroupBy(assignment => assignment.StepInstanceId)
                .ToDictionary(group => group.Key, group => group.First());
            assignmentsByRunId[runId] = loaded;
            return loaded;
        }
    }

    private async Task<IReadOnlyList<ProcessRuntimeActiveAgentProjection>> LoadActiveAgentsAsync(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var activeRuns = runs.Where(CanHaveActiveAgents).ToArray();
        if (runtimeStateStore is null || assignmentStore is null || activeRuns.Length == 0)
        {
            return [];
        }

        var activeAgents = new List<ProcessRuntimeActiveAgentProjection>();
        var claimAgents = new List<ProcessRuntimeActiveAgentProjection>();
        var observedStepKeys = new HashSet<(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId)>();
        var assignmentsByRun = new Dictionary<ProcessRunId, IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment>>();
        var observedCandidateRunIds = new HashSet<ProcessRunId>();
        var runById = activeRuns.ToDictionary(run => run.RunId);

        foreach (var run in activeRuns)
        {
            var assignments = await assignmentStore.LoadByRunAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            assignmentsByRun[run.RunId] = assignments
                .GroupBy(assignment => assignment.StepInstanceId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        foreach (var run in activeRuns)
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
                if (!assignmentsByStep.TryGetValue(step.StepInstanceId, out var assignment))
                {
                    continue;
                }

                if (!ProcessLaunchExecutorKinds.CanResolveAsAgent(assignment.ExecutorKind))
                {
                    continue;
                }

                observedCandidateRunIds.Add(run.RunId);
                var claim = step.ActiveClaimToken is { } activeClaimToken
                    ? state.Claims.FirstOrDefault(item => item.ClaimToken == activeClaimToken)
                    : null;
                var isLeaseExpired = claim is not null && claim.ExpiresAtUtc < nowUtc;
                var isWorking = !isLeaseExpired &&
                    step.Status is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running;
                claimAgents.Add(new ProcessRuntimeActiveAgentProjection(
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

        if (executionObservationReader is not null && observedCandidateRunIds.Count > 0)
        {
            var windowStartUtc = activeRuns
                .Where(run => observedCandidateRunIds.Contains(run.RunId))
                .Select(run => run.FirstEventAtUtc)
                .DefaultIfEmpty(nowUtc - ActiveExecutionStaleAfter)
                .Min()
                .AddMinutes(-5);
            var observations = await executionObservationReader.ListAsync(
                new ProcessExecutionObservationQuery(
                    observedCandidateRunIds.ToArray(),
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

        foreach (var claimAgent in claimAgents)
        {
            if (!observedStepKeys.Contains((new ProcessRunId(claimAgent.RunId), new ProcessStepInstanceId(claimAgent.StepInstanceId))))
            {
                activeAgents.Add(claimAgent);
            }
        }

        return activeAgents
            .OrderByDescending(agent => agent.IsWorking)
            .ThenByDescending(agent => string.Equals(agent.Status, nameof(ProcessRuntimeStepStatus.Running), StringComparison.Ordinal))
            .ThenByDescending(agent => agent.UpdatedAtUtc)
            .ThenBy(agent => agent.ExecutorDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ProcessLiveProcessSnapshot>> EnrichRunMetadataAsync(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        RuntimeRunEnrichmentCache enrichmentCache,
        CancellationToken cancellationToken)
    {
        if (runs.Count == 0)
        {
            return runs;
        }

        var enriched = new List<ProcessLiveProcessSnapshot>(runs.Count);
        foreach (var run in runs)
        {
            var assignmentsByStep = await enrichmentCache.LoadAssignmentsByStepAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            var assignments = assignmentsByStep.Values
                .OrderBy(assignment => assignment.StepKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var processName = string.IsNullOrWhiteSpace(run.ProcessName)
                ? ResolveProcessName(assignments)
                : run.ProcessName;
            var projectId = run.ProjectId ?? ResolveProjectId(assignments);
            var projectName = FirstNonEmpty(run.ProjectName, ResolveProjectName(assignments));
            var isSubprocess = run.IsSubprocess || IsSubprocess(assignments);

            enriched.Add(run with
            {
                ProcessName = processName,
                ProjectId = projectId,
                ProjectName = projectName,
                IsSubprocess = isSubprocess
            });
        }

        return enriched;
    }

    private async Task<IReadOnlyList<ProcessLiveProcessSnapshot>> ReconcileLiveActivityAsync(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        DateTimeOffset nowUtc,
        RuntimeRunEnrichmentCache enrichmentCache,
        CancellationToken cancellationToken)
    {
        if (!enrichmentCache.CanLoadStates || runs.Count == 0)
        {
            return runs;
        }

        var reconciled = new List<ProcessLiveProcessSnapshot>(runs.Count);
        foreach (var run in runs)
        {
            if (run.Status != ProcessProjectedRunStatus.NeedsAttention)
            {
                reconciled.Add(run);
                continue;
            }

            var state = await enrichmentCache.LoadStateAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                reconciled.Add(run);
                continue;
            }

            var isActive = HasOpenNonExpiredClaims(state, nowUtc);
            reconciled.Add(run.IsActive == isActive
                ? run
                : run with { IsActive = isActive });
        }

        return reconciled;
    }

    private async Task<IReadOnlyList<ProcessLiveProcessSnapshot>> EnrichOperatorActionsAsync(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        DateTimeOffset nowUtc,
        RuntimeRunEnrichmentCache enrichmentCache,
        CancellationToken cancellationToken)
    {
        if (!enrichmentCache.CanLoadStates || !enrichmentCache.CanLoadAssignments || runs.Count == 0)
        {
            return runs;
        }

        var enriched = new List<ProcessLiveProcessSnapshot>(runs.Count);
        foreach (var run in runs)
        {
            if (!CanHaveOperatorActions(run))
            {
                enriched.Add(run);
                continue;
            }

            var state = await enrichmentCache.LoadStateAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                enriched.Add(run);
                continue;
            }

            var assignmentsByStep = await enrichmentCache.LoadAssignmentsByStepAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            var hasOpenNonExpiredClaims = HasOpenNonExpiredClaims(state, nowUtc);
            var actionableSteps = new List<(ProcessRuntimeStepState Step, DispatchClaimState? ExpiredClaim)>();
            foreach (var step in state.Steps)
            {
                if (TryGetExpiredActiveClaim(state, step, nowUtc, out var expiredClaim))
                {
                    actionableSteps.Add((step, expiredClaim));
                }
            }

            if (!hasOpenNonExpiredClaims)
            {
                foreach (var step in state.Steps.Where(step => IsOperatorReworkCandidate(state, step)))
                {
                    actionableSteps.Add((step, null));
                }
            }

            var observationsByStep = actionableSteps.Count == 0
                ? new Dictionary<(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId), ProcessExecutionObservation>()
                : await LoadLatestExecutionObservationsByStepAsync(
                    [run],
                    actionableSteps
                        .Select(item => item.Step.StepInstanceId)
                        .Distinct()
                        .ToArray(),
                    nowUtc,
                    cancellationToken).ConfigureAwait(false);
            var actions = actionableSteps
                .OrderByDescending(item => item.ExpiredClaim is not null)
                .ThenByDescending(item => item.Step.Status == ProcessRuntimeStepStatus.Failed)
                .ThenByDescending(item => item.Step.Status == ProcessRuntimeStepStatus.Blocked)
                .ThenBy(item => ResolveStepKey(item.Step, assignmentsByStep), StringComparer.OrdinalIgnoreCase)
                .Select((item, index) =>
                {
                    observationsByStep.TryGetValue((run.RunId, item.Step.StepInstanceId), out var observation);
                    return CreateOperatorActionProjection(
                        run,
                        state,
                        item.Step,
                        assignmentsByStep,
                        item.ExpiredClaim,
                        observation,
                        primaryRootCause: index == 0);
                })
                .ToArray();

            enriched.Add(run with { OperatorActions = actions });
        }

        return enriched;
    }

    private async Task<IReadOnlyDictionary<(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId), ProcessExecutionObservation>> LoadLatestExecutionObservationsByStepAsync(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        IReadOnlyList<ProcessStepInstanceId> stepInstanceIds,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (executionObservationReader is null || runs.Count == 0)
        {
            return new Dictionary<(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId), ProcessExecutionObservation>();
        }

        var runIds = runs
            .Select(run => run.RunId)
            .Distinct()
            .ToArray();
        var runIdSet = runIds.ToHashSet();
        var windowStartUtc = runs
            .Select(run => run.FirstEventAtUtc)
            .DefaultIfEmpty(nowUtc - ActiveExecutionStaleAfter)
            .Min()
            .AddMinutes(-5);
        var observations = await executionObservationReader.ListAsync(
            new ProcessExecutionObservationQuery(
                runIds,
                windowStartUtc,
                nowUtc,
                OperatorActionObservationTakePerRun)
            {
                StepInstanceIds = stepInstanceIds
            },
            cancellationToken).ConfigureAwait(false);

        return observations
            .Where(observation => runIdSet.Contains(observation.RunId))
            .GroupBy(observation => (observation.RunId, observation.StepInstanceId))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(observation => observation.UpdatedAtUtc)
                    .First());
    }

    private async Task<IReadOnlyList<ProcessLiveProcessSnapshot>> EnrichCurrentStepsAsync(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        DateTimeOffset nowUtc,
        RuntimeRunEnrichmentCache enrichmentCache,
        CancellationToken cancellationToken)
    {
        if (!enrichmentCache.CanLoadStates || !enrichmentCache.CanLoadAssignments || runs.Count == 0)
        {
            return runs;
        }

        var enriched = new List<ProcessLiveProcessSnapshot>(runs.Count);
        foreach (var run in runs)
        {
            if (!CanHaveRuntimeDetails(run))
            {
                enriched.Add(ClearCurrentStep(run));
                continue;
            }

            var state = await enrichmentCache.LoadStateAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                enriched.Add(ClearCurrentStep(run));
                continue;
            }

            var runWithProgress = ApplyStepProgress(run, state);
            var assignmentsByStep = await enrichmentCache.LoadAssignmentsByStepAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            var currentStep = ResolveCurrentStep(state);
            if (currentStep is null)
            {
                enriched.Add(ClearCurrentStep(runWithProgress));
                continue;
            }

            assignmentsByStep.TryGetValue(currentStep.StepInstanceId, out var assignment);
            var stepLineage = BuildResultLineage(state, assignmentsByStep)
                .Where(item => item.StepInstanceId == currentStep.StepInstanceId.Value)
                .ToArray();
            var currentStepDiagnostics = BuildCurrentStepDiagnostics(currentStep, stepLineage);
            enriched.Add(runWithProgress with
            {
                CurrentStep = CreateCurrentStepProjection(
                    runWithProgress,
                    state,
                    currentStep,
                    assignment,
                    nowUtc,
                    currentStepDiagnostics,
                    stepLineage.SelectMany(item => item.ProducedArtifacts).ToArray())
            });
        }

        return enriched;
    }

    private async Task<IReadOnlyList<ProcessLiveProcessSnapshot>> EnrichLiveDiagnosticsAsync(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        RuntimeRunEnrichmentCache enrichmentCache,
        CancellationToken cancellationToken)
    {
        if (!enrichmentCache.CanLoadStates || runs.Count == 0)
        {
            return runs;
        }

        var enriched = new List<ProcessLiveProcessSnapshot>(runs.Count);
        foreach (var run in runs)
        {
            if (!CanHaveRuntimeDetails(run))
            {
                enriched.Add(run);
                continue;
            }

            var diagnostics = await BuildRunDiagnosticsAsync(
                run.RunId,
                enrichmentCache,
                cancellationToken).ConfigureAwait(false);
            if (diagnostics.Count == 0)
            {
                enriched.Add(run);
                continue;
            }

            enriched.Add(run with
            {
                Diagnostics = diagnostics,
                RecentEvents = EnrichLiveEvents(run.RecentEvents, diagnostics)
            });
        }

        return enriched;
    }

    private async Task<IReadOnlyList<ProcessLiveProcessSnapshot>> EnrichChildRunWaitsAsync(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        RuntimeRunEnrichmentCache enrichmentCache,
        CancellationToken cancellationToken)
    {
        if (!enrichmentCache.CanLoadStates || !enrichmentCache.CanLoadAssignments || assignmentStore is null || runs.Count == 0)
        {
            return runs;
        }

        var enriched = new List<ProcessLiveProcessSnapshot>(runs.Count);
        foreach (var run in runs)
        {
            if (!CanHaveRuntimeDetails(run))
            {
                enriched.Add(ClearChildRunWaits(run));
                continue;
            }

            var parentState = await enrichmentCache.LoadStateAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            if (parentState is null)
            {
                enriched.Add(ClearChildRunWaits(run));
                continue;
            }

            var parentAssignmentsByStep = await enrichmentCache.LoadAssignmentsByStepAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            var childAssignments = await assignmentStore.FindByLaunchVariablesAsync(
                ProcessRuntimeLaunchVariables.CreateParentRunLookup(run.RunId),
                cancellationToken).ConfigureAwait(false);
            if (childAssignments.Count == 0)
            {
                enriched.Add(ClearChildRunWaits(run));
                continue;
            }

            var childAssignmentsByParentStep = childAssignments
                .Select(assignment => new
                {
                    Assignment = assignment,
                    ParentStepId = ProcessRuntimeLaunchVariables.TryReadParentStepId(
                        assignment.LaunchVariables,
                        out var parentStepId)
                        ? parentStepId
                        : (ProcessStepInstanceId?)null
                })
                .Where(item => item.ParentStepId is not null)
                .GroupBy(item => item.ParentStepId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Assignment).ToArray() as IReadOnlyList<ProcessRuntimeStepAssignment>);
            if (childAssignmentsByParentStep.Count == 0)
            {
                enriched.Add(ClearChildRunWaits(run));
                continue;
            }

            var waits = new List<ProcessRuntimeChildRunWaitProjection>();
            foreach (var parentStep in parentState.Steps.Where(IsChildWaitParentStepCandidate))
            {
                if (!parentAssignmentsByStep.TryGetValue(parentStep.StepInstanceId, out var parentAssignment) ||
                    !childAssignmentsByParentStep.TryGetValue(parentStep.StepInstanceId, out var linkedChildAssignments))
                {
                    continue;
                }

                foreach (var childGroup in linkedChildAssignments.GroupBy(assignment => assignment.RunId))
                {
                    if (childGroup.Key == run.RunId)
                    {
                        continue;
                    }

                    var childState = await enrichmentCache.LoadStateAsync(childGroup.Key, cancellationToken).ConfigureAwait(false);
                    if (childState is null ||
                        ProcessRuntimeTerminalStates.IsRunTerminal(childState.Status) ||
                        childState.Status == ProcessRuntimeStatus.Blocked)
                    {
                        continue;
                    }

                    var childAssignmentsByStep = childGroup
                        .GroupBy(assignment => assignment.StepInstanceId)
                        .ToDictionary(group => group.Key, group => group.First());
                    var childStep = ResolveCurrentStep(childState);
                    ProcessRuntimeStepAssignment? childAssignment = null;
                    if (childStep is not null)
                    {
                        childAssignmentsByStep.TryGetValue(childStep.StepInstanceId, out childAssignment);
                    }

                    waits.Add(new ProcessRuntimeChildRunWaitProjection(
                        run.RunId.Value,
                        parentStep.StepInstanceId.Value,
                        parentAssignment.StepKey,
                        parentStep.Status.ToString(),
                        childState.RunId.Value,
                        childState.Status.ToString(),
                        childStep is null
                            ? null
                            : FirstNonEmpty(childAssignment?.StepKey, childStep.StepInstanceId.Value.ToString("D")),
                        childStep?.Status.ToString(),
                        BuildChildRunWaitSummary(parentAssignment, parentStep, childState, childStep, childAssignment)));
                }
            }

            enriched.Add(run with
            {
                WaitingOnChildRuns = waits
                    .OrderBy(wait => wait.ParentStepKey, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(wait => wait.ChildRunId)
                    .ToArray()
            });
        }

        return enriched;
    }

    private static ProcessLiveProcessSnapshot ClearCurrentStep(ProcessLiveProcessSnapshot run)
        => run.CurrentStep is null
            ? run
            : run with { CurrentStep = null };

    private static ProcessLiveProcessSnapshot ApplyStepProgress(
        ProcessLiveProcessSnapshot run,
        ProcessRuntimeStateSnapshot state)
    {
        var executableSteps = state.Steps.Where(step => step.IsExecutable).ToArray();
        if (executableSteps.Length == 0)
        {
            return run with
            {
                ExecutableStepCount = 0,
                CompletedStepCount = 0,
                TerminalStepCount = 0,
                ProgressLabel = "No executable steps"
            };
        }

        var completedStepCount = executableSteps.Count(step =>
            step.Status is ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Skipped);
        var terminalStepCount = executableSteps.Count(step => ProcessRuntimeTerminalStates.IsStepTerminal(step.Status));

        return run with
        {
            ExecutableStepCount = executableSteps.Length,
            CompletedStepCount = completedStepCount,
            TerminalStepCount = terminalStepCount,
            ProgressLabel = $"{completedStepCount.ToString(CultureInfo.InvariantCulture)} of {executableSteps.Length.ToString(CultureInfo.InvariantCulture)} executable steps complete"
        };
    }

    private static ProcessLiveProcessSnapshot ClearChildRunWaits(ProcessLiveProcessSnapshot run)
        => run.WaitingOnChildRuns is { Count: 0 }
            ? run
            : run with { WaitingOnChildRuns = [] };

    private static bool CanHaveOperatorActions(ProcessLiveProcessSnapshot run)
        => CanHaveRuntimeDetails(run) || run.Status is ProcessProjectedRunStatus.Failed;

    private static bool CanHaveRuntimeDetails(ProcessLiveProcessSnapshot run)
        => run.IsActive || run.Status is ProcessProjectedRunStatus.NeedsAttention;

    private static bool CanHaveActiveAgents(ProcessLiveProcessSnapshot run)
        => run.IsActive || run.Status is ProcessProjectedRunStatus.NeedsAttention;

    private static bool IsChildWaitParentStepCandidate(ProcessRuntimeStepState step)
        => step.IsExecutable &&
           !ProcessRuntimeTerminalStates.IsStepTerminal(step.Status) &&
           step.Status is (ProcessRuntimeStepStatus.Waiting or
               ProcessRuntimeStepStatus.Blocked or
               ProcessRuntimeStepStatus.Ready or
               ProcessRuntimeStepStatus.Claimed or
               ProcessRuntimeStepStatus.Running);

    private static bool IsCurrentStepCandidate(ProcessRuntimeStepState step)
    {
        if (!step.IsExecutable || ProcessRuntimeTerminalStates.IsStepTerminal(step.Status))
        {
            return false;
        }

        return step.Status is ProcessRuntimeStepStatus.Running or
               ProcessRuntimeStepStatus.Claimed or
               ProcessRuntimeStepStatus.Waiting or
               ProcessRuntimeStepStatus.Ready or
               ProcessRuntimeStepStatus.Pending ||
               step.AttemptNumber > 0 &&
               step.Status is (ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Blocked);
    }

    private static bool IsOperatorReworkCandidate(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step)
    {
        if (!step.IsExecutable ||
            step.ActiveClaimToken is not null ||
            step.Status is not (ProcessRuntimeStepStatus.Blocked or ProcessRuntimeStepStatus.Failed))
        {
            return false;
        }

        return step.Status != ProcessRuntimeStepStatus.Blocked ||
               BlockedStepCanBeReworked(state, step);
    }

    private static bool HasOpenNonExpiredClaims(ProcessRuntimeStateSnapshot state, DateTimeOffset nowUtc)
        => state.Claims.Any(claim =>
            claim.Status is (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) &&
            claim.ExpiresAtUtc > nowUtc);

    private static bool TryGetExpiredActiveClaim(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step,
        DateTimeOffset nowUtc,
        out DispatchClaimState? expiredClaim)
    {
        expiredClaim = null;
        if (!step.IsExecutable ||
            step.ActiveClaimToken is not { } activeClaimToken ||
            step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running))
        {
            return false;
        }

        var claim = state.Claims.FirstOrDefault(candidate => candidate.ClaimToken == activeClaimToken);
        if (claim is null ||
            claim.Status is not (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) ||
            claim.ExpiresAtUtc > nowUtc)
        {
            return false;
        }

        expiredClaim = claim;
        return true;
    }

    private static bool BlockedStepCanBeReworked(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step)
    {
        return ProcessRuntimeArtifactContracts.DependenciesSatisfied(state, step) &&
               ProcessRuntimeArtifactContracts.RequiredArtifactsAvailable(state, step);
    }

    private static ProcessRuntimeOperatorActionProjection CreateOperatorActionProjection(
        ProcessLiveProcessSnapshot run,
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step,
        IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment> assignmentsByStep,
        DispatchClaimState? expiredClaim,
        ProcessExecutionObservation? executionObservation,
        bool primaryRootCause)
    {
        assignmentsByStep.TryGetValue(step.StepInstanceId, out var assignment);
        var stepKey = ResolveStepKey(step, assignmentsByStep);
        var roleKey = FirstNonEmpty(assignment?.RoleKey, "unassigned");
        var roleDisplayName = FirstNonEmpty(assignment?.RoleDisplayName, roleKey);
        var executorDisplayName = FirstNonEmpty(assignment?.ExecutorDisplayName, "Unassigned executor");
        var receipt = state.AppliedResults
            .Where(item => item.StepInstanceId == step.StepInstanceId)
            .LastOrDefault();
        var diagnostic = ProcessRuntimeOperatorActionDiagnostics.Create(executionObservation);
        var packet = ProcessBlockedStepPacketBuilder.Create(
            stepKey,
            step,
            assignment,
            receipt,
            expiredClaim,
            diagnostic);
        var capabilityHint = BuildOperatorCapabilityHint(assignment, diagnostic);
        var problemSummary = $"{packet.ProblemSummary} {capabilityHint}".Trim();
        var requiredDecision = $"{packet.RequiredOperatorDecision} {capabilityHint}".Trim();
        var recommendedInstruction = $"{packet.RecommendedInstruction} {capabilityHint}".Trim();
        return new ProcessRuntimeOperatorActionProjection(
            run.RunId.Value,
            step.StepInstanceId.Value,
            stepKey,
            step.Status.ToString(),
            roleKey,
            roleDisplayName,
            executorDisplayName,
            ProcessRuntimeOperatorActionKind.RequestRework,
            expiredClaim is null ? "Approve rework" : "Retry expired claim",
            BuildOperatorActionSummary(stepKey, step.Status, roleDisplayName, executorDisplayName, receipt, diagnostic),
            IsEnabled: true,
            DisabledReason: null)
        {
            ProblemSummary = problemSummary,
            RequiredOperatorDecision = requiredDecision,
            RecommendedInstruction = recommendedInstruction,
            PrimaryRootCause = primaryRootCause
        };
    }

    private static string ResolveStepKey(
        ProcessRuntimeStepState step,
        IReadOnlyDictionary<ProcessStepInstanceId, ProcessRuntimeStepAssignment> assignmentsByStep)
    {
        return assignmentsByStep.TryGetValue(step.StepInstanceId, out var assignment) &&
               !string.IsNullOrWhiteSpace(assignment.StepKey)
            ? assignment.StepKey
            : step.StepInstanceId.ToString();
    }

    private static string BuildOperatorActionSummary(
        string stepKey,
        ProcessRuntimeStepStatus status,
        string roleDisplayName,
        string executorDisplayName,
        StrategyResultReceipt? receipt,
        StepExecutionDiagnostic? diagnostic)
    {
        var outcomeText = receipt is null ? string.Empty : $" Last strategy outcome: {receipt.Outcome}.";
        var diagnosticText = ProcessRuntimeOperatorActionDiagnostics.BuildExecutionSummary(diagnostic);
        return $"Root action: approve manager-guided rework for {stepKey} after {status}.{outcomeText} {diagnosticText} Assigned role: {roleDisplayName}. Executor: {executorDisplayName}.".Trim();
    }

    private static string BuildOperatorProblemSummary(
        string stepKey,
        ProcessRuntimeStepState step,
        string roleDisplayName,
        string executorDisplayName,
        StrategyResultReceipt? receipt,
        DispatchClaimState? expiredClaim,
        StepExecutionDiagnostic? diagnostic)
    {
        if (expiredClaim is not null)
        {
            var expiredAt = expiredClaim.ExpiresAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            return $"{stepKey} is still {step.Status}, but its dispatch lease expired at {expiredAt}. The runtime cannot accept a late result for the expired claim. Approve retry to expire the stale claim, return the step to Ready, and dispatch {roleDisplayName} again. Current executor: {executorDisplayName}.";
        }

        var outcomeText = receipt is null
            ? "The runtime has no stored strategy outcome for the current blocker."
            : $"The last strategy outcome was {receipt.Outcome} and the runtime applied {receipt.AppliedStepStatus}.";
        var attemptText = step.AttemptNumber <= 0
            ? "before a dispatch attempt was recorded"
            : $"on attempt {step.AttemptNumber.ToString(CultureInfo.InvariantCulture)}";
        var diagnosticText = ProcessRuntimeOperatorActionDiagnostics.BuildExecutionSummary(diagnostic);
        var missingDiagnosticText = receipt is null || diagnostic is not null
            ? string.Empty
            : " No AgentFramework result summary was found for this blocker; inspect execution runs by process run and step id before approving a blind retry.";

        return $"{stepKey} is {step.Status} {attemptText}. {outcomeText} {diagnosticText}{missingDiagnosticText} This is the actionable upstream step for role {roleDisplayName}, currently assigned to {executorDisplayName}.".Trim();
    }

    private static string BuildOperatorDecision(
        string stepKey,
        ProcessRuntimeStepStatus status,
        string roleDisplayName,
        string executorDisplayName,
        DispatchClaimState? expiredClaim,
        StepExecutionDiagnostic? diagnostic)
    {
        if (expiredClaim is not null)
        {
            return $"Retry {stepKey} by expiring the stale dispatch claim and letting the process manager dispatch {roleDisplayName} again. Current executor: {executorDisplayName}. Add an operator note if the agent needs extra context.";
        }

        if (diagnostic is not null &&
            IsRepairBranch(diagnostic.BranchOutcomeKey) &&
            string.Equals(diagnostic.Status, nameof(ProcessRuntimeStepStatus.Blocked), StringComparison.OrdinalIgnoreCase))
        {
            return $"Approve rework for {stepKey}, but instruct {roleDisplayName} to return a completed process-step outcome with branchOutcomeKey '{diagnostic.BranchOutcomeKey}' when the defect is repairable and evidence is complete. Keep Blocked only for confirmed tool, policy, infrastructure, or process-contract failures. Current executor: {executorDisplayName}.";
        }

        return $"Approve rework to return {stepKey} from {status} to Ready and let the process manager dispatch {roleDisplayName} again. Current executor: {executorDisplayName}. Add an operator note if the agent needs extra context.";
    }

    private static string BuildRecommendedOperatorInstruction(
        string stepKey,
        ProcessRuntimeStepState step,
        string roleDisplayName,
        string executorDisplayName,
        StrategyResultReceipt? receipt,
        DispatchClaimState? expiredClaim,
        StepExecutionDiagnostic? diagnostic)
    {
        if (expiredClaim is not null)
        {
            return $"Manager-approved retry for expired dispatch claim on step '{stepKey}'. Preserve any managed artifacts already written by {executorDisplayName}, verify they satisfy the output contract, produce the required evidence for role '{roleDisplayName}', and continue the process. Step status before retry: {step.Status}.";
        }

        var outcomeText = receipt is null
            ? "the previous blocker"
            : $"the previous {receipt.Outcome} outcome";
        var branchInstruction = diagnostic is not null && IsRepairBranch(diagnostic.BranchOutcomeKey)
            ? $" If the previous finding is repairable, return status Completed with branchOutcomeKey '{diagnostic.BranchOutcomeKey}' instead of Blocked."
            : string.Empty;
        var priorActions = ProcessRuntimeOperatorActionDiagnostics.BuildPriorNextActions(diagnostic);
        var failedToolInstruction = ProcessRuntimeOperatorActionDiagnostics.BuildFailedToolInstruction(diagnostic);

        return $"Manager-approved rework for step '{stepKey}'. Resolve {outcomeText}, preserve accepted upstream artifacts, produce the required evidence for role '{roleDisplayName}', and continue the process. Previous executor: {executorDisplayName}. Step status before rework: {step.Status}.{branchInstruction}{priorActions}{failedToolInstruction}";
    }

    private static bool IsRepairBranch(string branchOutcomeKey)
        => branchOutcomeKey.Contains("repair", StringComparison.OrdinalIgnoreCase);

    private static string BuildOperatorCapabilityHint(
        ProcessRuntimeStepAssignment? assignment,
        StepExecutionDiagnostic? diagnostic)
    {
        if (diagnostic is not null && diagnostic.HasConcreteFailure)
        {
            return "Why this may repeat: a blind retry will repeat the same failure unless the next attempt reads the listed diagnostic artifacts, fixes the concrete command target, environment, or product issue, and records fresh successful receipts.";
        }

        if (assignment is null)
        {
            return "Why this may repeat: runtime assignment metadata is missing, so verify the step still has a valid agent binding before approving another attempt.";
        }

        var operations = assignment.AllowedOperations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(operation => operation, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var operationSummary = operations.Length == 0
            ? "the step's required operations"
            : string.Join(", ", operations);
        var requiredEvidence = assignment.RequiredArtifactSlotIds.Count == 0
            ? "the step's required evidence"
            : $"{assignment.RequiredArtifactSlotIds.Count.ToString(CultureInfo.InvariantCulture)} required artifact slot(s)";

        return $"Why this may repeat: if the next attempt fails the same way, verify that {assignment.ExecutorDisplayName} has the tools, MCP servers, skills, and project access needed for {operationSummary}, and that the agent can write {requiredEvidence} before returning completed output.";
    }

    private static bool IsAgentActiveStep(ProcessRuntimeStepState step)
        => step.Status is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running;

    private static string ResolveProcessName(IEnumerable<ProcessRuntimeStepAssignment> assignments)
    {
        foreach (var assignment in assignments)
        {
            if (ProcessRuntimeLaunchVariables.TryReadProcessDefinitionName(assignment.LaunchVariables, out var definitionName))
            {
                return definitionName;
            }
        }

        return string.Empty;
    }

    private static Guid? ResolveProjectId(IEnumerable<ProcessRuntimeStepAssignment> assignments)
    {
        foreach (var assignment in assignments)
        {
            if (ProcessRuntimeLaunchVariables.TryReadProjectId(assignment.LaunchVariables, out var projectId))
            {
                return projectId;
            }
        }

        return null;
    }

    private static string ResolveProjectName(IEnumerable<ProcessRuntimeStepAssignment> assignments)
    {
        foreach (var assignment in assignments)
        {
            if (ProcessRuntimeLaunchVariables.TryReadProjectName(assignment.LaunchVariables, out var projectName))
            {
                return projectName;
            }
        }

        return string.Empty;
    }

    private static bool IsSubprocess(IEnumerable<ProcessRuntimeStepAssignment> assignments)
    {
        foreach (var assignment in assignments)
        {
            if (ProcessRuntimeLaunchVariables.TryReadParentRunId(assignment.LaunchVariables, out _))
            {
                return true;
            }
        }

        return false;
    }

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
            AgentAvatarImageUrl = observation.AgentAvatarImageUrl,
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

    private static ProcessRuntimeCurrentStepProjection CreateCurrentStepProjection(
        ProcessLiveProcessSnapshot run,
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step,
        ProcessRuntimeStepAssignment? assignment,
        DateTimeOffset nowUtc,
        IReadOnlyList<ProcessRuntimeDiagnosticProjection> diagnostics,
        IReadOnlyList<ProcessRuntimeArtifactLineageProjection> producedArtifacts)
    {
        var stepKey = FirstNonEmpty(assignment?.StepKey, step.StepInstanceId.Value.ToString("D"));
        var roleKey = FirstNonEmpty(assignment?.RoleKey, "unassigned");
        var roleDisplayName = FirstNonEmpty(assignment?.RoleDisplayName, roleKey);
        var executorDisplayName = FirstNonEmpty(assignment?.ExecutorDisplayName, "Unassigned executor");
        var claim = step.ActiveClaimToken is { } activeClaimToken
            ? state.Claims.FirstOrDefault(item => item.ClaimToken == activeClaimToken)
            : null;
        var isLeaseExpired = claim is not null && claim.ExpiresAtUtc < nowUtc;
        var isClaimOpen = claim?.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed;
        var isWorking = isClaimOpen &&
            !isLeaseExpired &&
            step.Status is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running;

        return new ProcessRuntimeCurrentStepProjection(
            run.RunId.Value,
            step.StepInstanceId.Value,
            stepKey,
            step.Status.ToString(),
            roleKey,
            roleDisplayName,
            executorDisplayName,
            step.AttemptNumber,
            isWorking,
            isLeaseExpired,
            state.UpdatedAtUtc,
            claim?.CreatedAtUtc,
            claim?.ExpiresAtUtc,
            BuildCurrentStepSummary(stepKey, step, roleDisplayName, executorDisplayName, claim, isLeaseExpired))
        {
            Diagnostics = diagnostics,
            ProducedArtifacts = producedArtifacts
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

    private static ProcessRuntimeStepState? ResolveCurrentStep(ProcessRuntimeStateSnapshot state)
        => state.Steps
            .Where(IsCurrentStepCandidate)
            .OrderBy(step => ResolveCurrentStepPriority(step))
            .ThenByDescending(step => step.AttemptNumber)
            .ThenBy(step => step.StepInstanceId.Value)
            .FirstOrDefault();

    private static int ResolveCurrentStepPriority(ProcessRuntimeStepState step)
        => step.Status switch
        {
            ProcessRuntimeStepStatus.Running => 0,
            ProcessRuntimeStepStatus.Claimed => 1,
            ProcessRuntimeStepStatus.Waiting => 2,
            ProcessRuntimeStepStatus.Failed when step.AttemptNumber > 0 => 3,
            ProcessRuntimeStepStatus.Blocked when step.AttemptNumber > 0 => 4,
            ProcessRuntimeStepStatus.Ready => 5,
            ProcessRuntimeStepStatus.Pending => 6,
            ProcessRuntimeStepStatus.Failed => 7,
            ProcessRuntimeStepStatus.Blocked => 8,
            _ => 9
        };

    private static string BuildCurrentStepSummary(
        string stepKey,
        ProcessRuntimeStepState step,
        string roleDisplayName,
        string executorDisplayName,
        DispatchClaimState? claim,
        bool isLeaseExpired)
    {
        var attemptText = step.AttemptNumber <= 0
            ? "before a dispatch attempt"
            : $"on attempt {step.AttemptNumber.ToString(CultureInfo.InvariantCulture)}";
        var claimSummary = claim is null
            ? "No active dispatch lease is attached."
            : isLeaseExpired
                ? $"Lease expired {claim.ExpiresAtUtc.LocalDateTime:g}."
                : $"Lease expires {claim.ExpiresAtUtc.LocalDateTime:g}.";

        return $"{stepKey} is {step.Status} {attemptText}. Role: {roleDisplayName}. Executor: {executorDisplayName}. {claimSummary}";
    }

    private static string BuildChildRunWaitSummary(
        ProcessRuntimeStepAssignment parentAssignment,
        ProcessRuntimeStepState parentStep,
        ProcessRuntimeStateSnapshot childState,
        ProcessRuntimeStepState? childStep,
        ProcessRuntimeStepAssignment? childAssignment)
    {
        var childRunLabel = childState.RunId.Value.ToString("N")[..8];
        if (childStep is null)
        {
            return $"{parentAssignment.StepKey} is {parentStep.Status} while waiting on child run {childRunLabel}; child run is {childState.Status}.";
        }

        var childStepKey = FirstNonEmpty(childAssignment?.StepKey, childStep.StepInstanceId.Value.ToString("D"));
        return $"{parentAssignment.StepKey} is {parentStep.Status} while waiting on child run {childRunLabel}; child step {childStepKey} is {childStep.Status}.";
    }

    private static bool IsExecutionTerminal(string state)
        => string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

}
