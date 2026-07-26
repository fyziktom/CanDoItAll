using System.Globalization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessWorkspaceShellProjectionService(
    IProcessProjectionClock clock,
    ProcessDefinitionCatalogProjectionService definitionCatalogProjectionService,
    ProcessDefinitionEditorProjectionService definitionEditorProjectionService,
    ProcessDefinitionRoleEditorProjectionService definitionRoleEditorProjectionService,
    ProcessDefinitionCanvasEditorProjectionService definitionCanvasEditorProjectionService,
    ProcessDefinitionStepEditorProjectionService definitionStepEditorProjectionService,
    ProcessTemplateCatalogProjectionService templateCatalogProjectionService,
    ProcessRuntimeProjectionQueryService? runtimeProjectionQueryService = null,
    ProcessRuntimeProjectionCatchupService? projectionCatchupService = null,
    IProcessRuntimeUsageTelemetryReader? runtimeUsageTelemetryReader = null)
{
    private const string WorkspaceContextPrefix = "processes:workspace";
    private const string ProjectContextPrefix = "processes:project";
    private const string RunContextSegment = "run";
    private const string LaunchPlanContextSegment = "launch-plan";

    public async Task<ProcessWorkspaceShellProjection> GetShellAsync(
        ProcessWorkspaceShellRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequest(request);

        var observedAtUtc = clock.GetUtcNow();
        var authorization = new ProcessWorkspaceAuthorizationProjection(
            CanReadDefinitions: true,
            CanRefreshProjections: true,
            CanOpenAgentContext: true,
            CanEditDefinitions: false,
            CanLaunchRuns: true);
        var runtimeWorkspace = await LoadRuntimeWorkspaceAsync(request, observedAtUtc, cancellationToken).ConfigureAwait(false);

        var definitionLoadOptions = request.DefinitionLoadOptions ?? ProcessDefinitionWorkspaceLoadOptions.Full;
        var definitionCatalogQuery = ResolveDefinitionCatalogQuery(
            request.Selection,
            request.DefinitionCatalogQuery,
            out var hasProcessSelectionConflict);
        var definitionCatalog = await definitionCatalogProjectionService
            .GetCatalogAsync(request.Scope, definitionCatalogQuery, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (hasProcessSelectionConflict)
        {
            definitionCatalog = definitionCatalog with
            {
                SelectedDefinitionKey = request.DefinitionCatalogQuery.SelectedDefinitionKey,
                SelectedItem = null,
                SelectedEditor = null
            };
        }

        ProcessDefinitionEditorProjection? selectedEditor = null;
        if (definitionLoadOptions.IncludeSelectedEditor && definitionCatalog.SelectedItem is { } selectedItem)
        {
            selectedEditor = await definitionEditorProjectionService
                .GetEditorAsync(request.Scope, selectedItem.Key, cancellationToken)
                .ConfigureAwait(false);
        }
        if (selectedEditor is not null)
        {
            var selectedStepEditor = definitionLoadOptions.IncludeStepEditor || definitionLoadOptions.IncludeTemplateCatalog
                ? await definitionStepEditorProjectionService
                    .GetEditorAsync(request.Scope, selectedEditor.DefinitionKey, cancellationToken)
                    .ConfigureAwait(false)
                : null;
            selectedEditor = selectedEditor with
            {
                RoleEditor = definitionLoadOptions.IncludeRoleEditor
                    ? await definitionRoleEditorProjectionService
                        .GetEditorAsync(request.Scope, selectedEditor.DefinitionKey, cancellationToken)
                        .ConfigureAwait(false)
                    : null,
                Canvas = definitionLoadOptions.IncludeCanvas
                    ? await definitionCanvasEditorProjectionService
                        .GetCanvasAsync(request.Scope, selectedEditor.DefinitionKey, cancellationToken)
                        .ConfigureAwait(false)
                    : null,
                StepEditor = definitionLoadOptions.IncludeStepEditor ? selectedStepEditor : null,
                TemplateCatalog = definitionLoadOptions.IncludeTemplateCatalog && selectedStepEditor is not null
                    ? await templateCatalogProjectionService
                        .GetCatalogAsync(request.Scope, selectedEditor.DefinitionKey, request.TemplateCatalogQuery, selectedStepEditor, cancellationToken)
                        .ConfigureAwait(false)
                    : null
            };
        }

        definitionCatalog = definitionCatalog with
        {
            SelectedEditor = selectedEditor
        };

        return new ProcessWorkspaceShellProjection(
            request.Scope,
            request.Selection,
            ResolveTitle(request.Scope),
            ResolveSubtitle(request.Scope),
            definitionCatalog,
            CreateLiveRunSummary(runtimeWorkspace),
            CreateRefreshProjection(request.ForceRefresh, observedAtUtc, runtimeWorkspace.Freshness),
            authorization,
            CreateTabs(definitionCatalog, runtimeWorkspace),
            CreateCommands(authorization),
            CreateAgentEntry(request.Scope, request.Selection, authorization))
        {
            Runtime = runtimeWorkspace
        };
    }

    private ProcessDefinitionCatalogQueryProjection ResolveDefinitionCatalogQuery(
        ProcessWorkspaceSelectionProjection selection,
        ProcessDefinitionCatalogQueryProjection query,
        out bool hasSelectionConflict)
    {
        hasSelectionConflict = false;
        if (selection.ProcessId is not { } processId)
        {
            return query;
        }

        var definitionKey = definitionCatalogProjectionService.ResolveDefinitionKey(
            new ProcessDefinitionId(processId));
        if (string.IsNullOrWhiteSpace(definitionKey))
        {
            hasSelectionConflict = true;
            return query;
        }

        var processDefinitionKey = new ProcessDefinitionCatalogItemKey(definitionKey);
        if (query.SelectedDefinitionKey is { } requestedDefinitionKey &&
            requestedDefinitionKey != processDefinitionKey)
        {
            hasSelectionConflict = true;
            return query;
        }

        return query with
        {
            SelectedDefinitionKey = processDefinitionKey
        };
    }

    public Task<ProcessDefinitionCatalogCommandReceipt> FeedDefaultDefinitionsAsync(
        ProcessDefinitionFeedDefaultsCommand command,
        CancellationToken cancellationToken = default)
        => definitionCatalogProjectionService.FeedDefaultDefinitionsAsync(command, cancellationToken);

    public Task<ProcessDefinitionEditorCommandResult> ExecuteDefinitionEditorCommandAsync(
        ProcessDefinitionEditorCommand command,
        CancellationToken cancellationToken = default)
        => definitionEditorProjectionService.ExecuteCommandAsync(command, cancellationToken);

    public Task<ProcessDefinitionRoleEditorCommandResult> ExecuteDefinitionRoleEditorCommandAsync(
        ProcessDefinitionRoleEditorCommand command,
        CancellationToken cancellationToken = default)
        => definitionRoleEditorProjectionService.ExecuteCommandAsync(command, cancellationToken);

    public Task<ProcessDefinitionCanvasCommandResult> ExecuteDefinitionCanvasCommandAsync(
        ProcessDefinitionCanvasCommand command,
        CancellationToken cancellationToken = default)
        => definitionCanvasEditorProjectionService.ExecuteCommandAsync(command, cancellationToken);

    public Task<ProcessDefinitionStepEditorCommandResult> ExecuteDefinitionStepEditorCommandAsync(
        ProcessDefinitionStepEditorCommand command,
        CancellationToken cancellationToken = default)
        => definitionStepEditorProjectionService.ExecuteCommandAsync(command, cancellationToken);

    public async Task<ProcessTemplateImportCommandResult> ExecuteTemplateImportCommandAsync(
        ProcessTemplateImportCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);

        var stepEditor = await definitionStepEditorProjectionService
            .GetEditorAsync(command.Scope, command.TargetDefinitionKey, cancellationToken)
            .ConfigureAwait(false);
        return await templateCatalogProjectionService
            .ExecuteCommandAsync(command, stepEditor, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ValidateRequest(ProcessWorkspaceShellRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);
        ArgumentNullException.ThrowIfNull(request.Selection);
        ArgumentNullException.ThrowIfNull(request.DefinitionCatalogQuery);
        ArgumentNullException.ThrowIfNull(request.TemplateCatalogQuery);

        if (request.Scope.Kind == ProcessWorkspaceScopeKind.Project &&
            request.Scope.ProjectId is null)
        {
            throw new ArgumentException("Project-scoped process workspace requires a project id.", nameof(request));
        }

        if (request.Scope.Kind == ProcessWorkspaceScopeKind.Global &&
            request.Scope.ProjectId is not null)
        {
            throw new ArgumentException("Global process workspace cannot carry a project id.", nameof(request));
        }
    }

    private static string ResolveTitle(ProcessWorkspaceShellScope scope)
        => scope.Kind == ProcessWorkspaceScopeKind.Project
            ? "Project processes"
            : "Processes";

    private static string ResolveSubtitle(ProcessWorkspaceShellScope scope)
        => scope.Kind == ProcessWorkspaceScopeKind.Project
            ? $"Projection-first project workspace for {scope.ProjectId:D}."
            : "Projection-first workspace for definitions, launches, live runs, and history.";

    private async Task<ProcessRuntimeWorkspaceProjection> LoadRuntimeWorkspaceAsync(
        ProcessWorkspaceShellRequest request,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        if (runtimeProjectionQueryService is null)
        {
            return ProcessRuntimeWorkspaceProjection.Empty;
        }

        if (request.ForceRefresh && projectionCatchupService is not null)
        {
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
        }

        var runtimeQuery = NormalizeRuntimeQuery(request);
        var result = await runtimeProjectionQueryService
            .GetRuntimeWorkspaceAsync(
                new ProcessRuntimeWorkspaceQuery(
                    observedAtUtc,
                    ResolveHistoryWindow(runtimeQuery.HistoryWindow),
                    runtimeQuery.EventPage,
                    runtimeQuery.EventPageSize,
                    TakeRuns: Math.Clamp(runtimeQuery.TakeRuns, 1, 100),
                    ResolveRunId(runtimeQuery.SelectedRunId ?? request.Selection.RunId),
                    runtimeQuery.AutoSelectRun,
                    runtimeQuery.LoadOptions ?? ProcessRuntimeWorkspaceLoadOptions.Full)
                {
                    PreviouslyLoadedRuns = runtimeQuery.PreviouslyLoadedRuns
                },
                cancellationToken)
            .ConfigureAwait(false);

        var usageObservations = await LoadRuntimeUsageObservationsAsync(
                runtimeQuery,
                result,
                observedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);

        return CreateRuntimeWorkspace(runtimeQuery, result, usageObservations);
    }

    private async Task<IReadOnlyList<ProcessRuntimeUsageObservation>> LoadRuntimeUsageObservationsAsync(
        ProcessRuntimeWorkspaceQueryProjection runtimeQuery,
        ProcessRuntimeWorkspaceResult result,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var loadOptions = runtimeQuery.LoadOptions ?? ProcessRuntimeWorkspaceLoadOptions.Full;
        if (!loadOptions.IncludeUsageTelemetry ||
            runtimeUsageTelemetryReader is null ||
            result.Runs.Count == 0 ||
            CanUseTerminalRecord(result))
        {
            return [];
        }

        var runIds = ResolveUsageTelemetryRunIds(runtimeQuery, result);

        if (runIds.Count == 0)
        {
            return [];
        }

        var historyWindow = ResolveHistoryWindow(runtimeQuery.HistoryWindow);
        return await runtimeUsageTelemetryReader.ListAsync(
                new ProcessRuntimeUsageTelemetryQuery(
                    runIds.ToArray(),
                    observedAtUtc.Subtract(historyWindow),
                    observedAtUtc,
                    ResolveUsageTelemetryTakePerRun(runtimeQuery.HistoryWindow)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static HashSet<ProcessRunId> ResolveUsageTelemetryRunIds(
        ProcessRuntimeWorkspaceQueryProjection runtimeQuery,
        ProcessRuntimeWorkspaceResult result)
    {
        var runIds = new HashSet<ProcessRunId>();
        var selectedRunId = result.SelectedRun?.RunId ?? ResolveRunId(runtimeQuery.SelectedRunId);
        if (selectedRunId is not { } selected)
        {
            foreach (var run in result.Runs)
            {
                runIds.Add(run.RootRunId);
                runIds.Add(run.RunId);
            }

            return runIds;
        }

        var selectedRootRunId = result.SelectedRun?.RootRunId ??
            result.Runs.FirstOrDefault(run => run.RunId == selected)?.RootRunId ??
            selected;
        foreach (var run in result.Runs.Where(run =>
                     run.RootRunId == selectedRootRunId ||
                     run.RunId == selectedRootRunId ||
                     run.RunId == selected))
        {
            runIds.Add(run.RootRunId);
            runIds.Add(run.RunId);
        }

        runIds.Add(selectedRootRunId);
        runIds.Add(selected);
        return runIds;
    }

    private static ProcessRuntimeWorkspaceQueryProjection NormalizeRuntimeQuery(ProcessWorkspaceShellRequest request)
    {
        var query = request.RuntimeQuery ?? new ProcessRuntimeWorkspaceQueryProjection(
            ProcessRuntimeHistoryWindow.ThirtyDays,
            EventPage: 0,
            EventPageSize: 25,
            request.Selection.RunId);

        return query with
        {
            EventPage = Math.Max(0, query.EventPage),
            EventPageSize = Math.Clamp(query.EventPageSize, 5, 200),
            TakeRuns = Math.Clamp(query.TakeRuns, 1, 100)
        };
    }

    private static int ResolveUsageTelemetryTakePerRun(ProcessRuntimeHistoryWindow historyWindow)
        => historyWindow switch
        {
            ProcessRuntimeHistoryWindow.LiveHour => 250,
            ProcessRuntimeHistoryWindow.OneDay => 1_000,
            ProcessRuntimeHistoryWindow.SevenDays => 3_000,
            ProcessRuntimeHistoryWindow.ThirtyDays => 5_000,
            _ => 1_000
        };

    private static ProcessRunId? ResolveRunId(Guid? runId)
        => runId.HasValue && runId.Value != Guid.Empty
            ? new ProcessRunId(runId.Value)
            : null;

    private static TimeSpan ResolveHistoryWindow(ProcessRuntimeHistoryWindow historyWindow)
        => historyWindow switch
        {
            ProcessRuntimeHistoryWindow.LiveHour => TimeSpan.FromHours(1),
            ProcessRuntimeHistoryWindow.OneDay => TimeSpan.FromDays(1),
            ProcessRuntimeHistoryWindow.SevenDays => TimeSpan.FromDays(7),
            ProcessRuntimeHistoryWindow.ThirtyDays => TimeSpan.FromDays(30),
            _ => TimeSpan.FromDays(1)
        };

    private static ProcessRuntimeWorkspaceProjection CreateRuntimeWorkspace(
        ProcessRuntimeWorkspaceQueryProjection query,
        ProcessRuntimeWorkspaceResult result,
        IReadOnlyList<ProcessRuntimeUsageObservation> usageObservations)
    {
        var events = result.Events;
        var metricEvents = result.MetricEvents;
        var selectedRunId = result.SelectedRun?.RunId.Value ?? query.SelectedRunId;
        var incidents = result.Runs
            .SelectMany(run => run.Incidents)
            .OrderByDescending(incident => incident.RaisedAtUtc)
            .ToArray();
        var managerMessages = BuildManagerMessages(metricEvents);
        var selectedRunRecord = result.SelectedRunRecord;
        var hasTerminalRecord = CanUseTerminalRecord(result);
        var hasDurableFacts = CanUseDurableFacts(result);
        var stats = hasTerminalRecord
            ? BuildRuntimeStats(result.Runs, selectedRunRecord!)
            : BuildRuntimeStats(result.Runs, metricEvents, usageObservations);

        return new ProcessRuntimeWorkspaceProjection(
            query.HistoryWindow,
            query.EventPage,
            query.EventPageSize,
            result.HasMoreEvents,
            selectedRunId,
            result.SelectedRun,
            result.Runs,
            events,
            incidents,
            managerMessages,
            result.ActiveAgents,
            stats,
            hasDurableFacts
                ? BuildMetricPoints(selectedRunRecord!)
                : hasTerminalRecord
                    ? []
                    : BuildMetricPoints(metricEvents, usageObservations),
            hasTerminalRecord
                ? BuildToolUsage(selectedRunRecord!)
                : BuildToolUsage(metricEvents),
            result.Freshness,
            hasTerminalRecord
                ? BuildRuntimeSummary(selectedRunRecord!)
                : BuildRuntimeSummary(result.Runs, metricEvents),
            hasTerminalRecord
                ? BuildAttentionSummary(selectedRunRecord!)
                : BuildAttentionSummary(result.Runs, incidents, metricEvents, result.SelectedRun, result.ActiveAgents))
        {
            SelectedRunRecord = selectedRunRecord,
            ReusableRuns = result.ReusableRuns
        };
    }

    private static bool CanUseDurableFacts(ProcessRuntimeWorkspaceResult result)
    {
        return CanUseTerminalRecord(result) &&
            result.SelectedRunRecord is
            {
                Summary.FactsStatus: ProcessRunFactsStatus.Completed,
                Facts: not null
            };
    }

    private static bool CanUseTerminalRecord(ProcessRuntimeWorkspaceResult result)
    {
        if (result.SelectedRunRecord is not { } selectedRunRecord)
        {
            return false;
        }

        var selectedRunId = result.SelectedRun?.RunId ?? selectedRunRecord.Summary.Identity.RunId;
        return result.Runs.FirstOrDefault(run => run.RunId == selectedRunId)?.IsActive != true;
    }

    private static ProcessLiveRunSummaryProjection CreateLiveRunSummary(ProcessRuntimeWorkspaceProjection runtime)
    {
        if (runtime == ProcessRuntimeWorkspaceProjection.Empty)
        {
            return new ProcessLiveRunSummaryProjection(
                ActiveRunCount: 0,
                AttentionRunCount: 0,
                FailedRunCount: 0,
                LastEventAtUtc: null,
                Summary: "Runtime projection store is not registered for this workspace shell.");
        }

        var active = runtime.Runs.Count(run => run.IsActive);
        var attention = runtime.Runs.Count(run => run.Status == ProcessProjectedRunStatus.NeedsAttention);
        var failed = runtime.Runs.Count(run => run.Status == ProcessProjectedRunStatus.Failed);
        var lastEventAtUtc = runtime.Runs.Count == 0
            ? (DateTimeOffset?)null
            : runtime.Runs.Max(run => run.LastEventAtUtc);

        return new ProcessLiveRunSummaryProjection(
            active,
            attention,
            failed,
            lastEventAtUtc,
            runtime.Runs.Count == 0
                ? "No runtime runs are present in the current projection window."
                : $"{active.ToString(CultureInfo.InvariantCulture)} active run(s), {attention.ToString(CultureInfo.InvariantCulture)} needing attention, {failed.ToString(CultureInfo.InvariantCulture)} failed.");
    }

    private static ProcessRuntimeStatsProjection BuildRuntimeStats(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        IReadOnlyList<ProcessTimelineEventProjection> events,
        IReadOnlyList<ProcessRuntimeUsageObservation> usageObservations)
    {
        var durationMs = events.Count < 2
            ? 0
            : checked((long)Math.Max(0, (events[^1].OccurredAtUtc - events[0].OccurredAtUtc).TotalMilliseconds));

        return new ProcessRuntimeStatsProjection(
            runs.Count,
            runs.Count(run => run.IsActive),
            runs.Count(run => run.Status == ProcessProjectedRunStatus.NeedsAttention),
            runs.Count(run => run.Status == ProcessProjectedRunStatus.Failed),
            events.Count,
            events.Count(IsManagerEvent),
            events.Count(IsToolUsageEvent),
            durationMs,
            InputTokens: usageObservations.Sum(observation => observation.InputTokens),
            CachedInputTokens: usageObservations.Sum(observation => observation.CachedInputTokens),
            OutputTokens: usageObservations.Sum(observation => observation.OutputTokens),
            TotalTokens: usageObservations.Sum(ResolveTotalTokens),
            EstimatedCost: decimal.Round(usageObservations.Sum(observation => observation.EstimatedCostUsd), 6, MidpointRounding.AwayFromZero),
            ActualCost: decimal.Round(usageObservations.Sum(observation => observation.ActualCostUsd), 6, MidpointRounding.AwayFromZero));
    }

    private static ProcessRuntimeStatsProjection BuildRuntimeStats(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        ProcessRunRecord record)
    {
        var metrics = record.Summary.Metrics;
        var totalEventCount = record.Facts?.TotalRuntimeEventCount ?? 0;
        var managerEventCount = record.Facts?.ManagerRuntimeEventCount ?? 0;
        return new ProcessRuntimeStatsProjection(
            runs.Count,
            runs.Count(run => run.IsActive),
            runs.Count(run => run.Status == ProcessProjectedRunStatus.NeedsAttention),
            runs.Count(run => run.Status == ProcessProjectedRunStatus.Failed),
            totalEventCount,
            managerEventCount,
            metrics.ToolCallCount,
            Math.Max(0, metrics.DurationMilliseconds ?? 0),
            SaturateToInt(metrics.InputTokenCount),
            SaturateToInt(metrics.CachedInputTokenCount),
            SaturateToInt(metrics.OutputTokenCount),
            SaturateToInt(metrics.TotalTokenCount),
            decimal.Round(metrics.EstimatedCost, 6, MidpointRounding.AwayFromZero),
            decimal.Round(metrics.ActualCost, 6, MidpointRounding.AwayFromZero));
    }

    private static IReadOnlyList<ProcessRuntimeMetricPointProjection> BuildMetricPoints(
        IReadOnlyList<ProcessTimelineEventProjection> events,
        IReadOnlyList<ProcessRuntimeUsageObservation> usageObservations)
    {
        if (events.Count == 0 && usageObservations.Count == 0)
        {
            return [];
        }

        var buckets = new Dictionary<DateTimeOffset, RuntimeMetricAccumulator>();
        foreach (var runtimeEvent in events)
        {
            var bucket = TruncateToMinute(runtimeEvent.OccurredAtUtc);
            if (!buckets.TryGetValue(bucket, out var accumulator))
            {
                accumulator = new RuntimeMetricAccumulator(bucket);
                buckets.Add(bucket, accumulator);
            }

            accumulator.Add(runtimeEvent);
        }

        foreach (var usageObservation in usageObservations)
        {
            var bucket = TruncateToMinute(usageObservation.CreatedAtUtc);
            if (!buckets.TryGetValue(bucket, out var accumulator))
            {
                accumulator = new RuntimeMetricAccumulator(bucket);
                buckets.Add(bucket, accumulator);
            }

            accumulator.Add(usageObservation);
        }

        var points = new List<ProcessRuntimeMetricPointProjection>(buckets.Count);
        foreach (var accumulator in buckets.Values)
        {
            points.Add(accumulator.ToProjection());
        }

        points.Sort(static (left, right) => left.TimestampUtc.CompareTo(right.TimestampUtc));
        return points;
    }

    private static IReadOnlyList<ProcessRuntimeMetricPointProjection> BuildMetricPoints(
        ProcessRunRecord record)
    {
        var facts = record.Facts
            ?? throw new InvalidOperationException(
                $"Process run '{record.Summary.Identity.RunId}' does not have hard facts for metric projection.");
        var buckets =
            new Dictionary<DateTimeOffset, DurableRuntimeMetricAccumulator>();
        foreach (var runtimeEventBucket in facts.RuntimeEventMinuteBuckets)
        {
            var accumulator = new DurableRuntimeMetricAccumulator(
                runtimeEventBucket.MinuteUtc);
            accumulator.Add(runtimeEventBucket);
            buckets.Add(runtimeEventBucket.MinuteUtc, accumulator);
        }

        foreach (var step in facts.Steps)
        {
            var minuteUtc = TruncateToMinute(
                step.EndedAtUtc ??
                step.StartedAtUtc ??
                record.Summary.Metrics.EndedAtUtc);
            if (!buckets.TryGetValue(minuteUtc, out var accumulator))
            {
                accumulator = new DurableRuntimeMetricAccumulator(minuteUtc);
                buckets.Add(minuteUtc, accumulator);
            }

            accumulator.Add(step);
        }

        return buckets.Values
            .OrderBy(accumulator => accumulator.TimestampUtc)
            .Select(accumulator => accumulator.ToProjection())
            .ToArray();
    }

    private static IReadOnlyList<ProcessRuntimeToolUsageProjection> BuildToolUsage(
        IReadOnlyList<ProcessTimelineEventProjection> events)
    {
        if (events.Count == 0)
        {
            return [];
        }

        var tools = new Dictionary<string, RuntimeToolUsageAccumulator>(StringComparer.Ordinal);
        foreach (var runtimeEvent in events)
        {
            if (!IsToolUsageEvent(runtimeEvent))
            {
                continue;
            }

            var toolName = NormalizeEventType(runtimeEvent.EventType);
            if (!tools.TryGetValue(toolName, out var accumulator))
            {
                accumulator = new RuntimeToolUsageAccumulator(toolName);
                tools.Add(toolName, accumulator);
            }

            accumulator.Add(runtimeEvent);
        }

        var usage = new List<ProcessRuntimeToolUsageProjection>(tools.Count);
        foreach (var accumulator in tools.Values)
        {
            usage.Add(accumulator.ToProjection());
        }

        usage.Sort(static (left, right) =>
        {
            var callCountComparison = right.CallCount.CompareTo(left.CallCount);
            return callCountComparison != 0
                ? callCountComparison
                : string.Compare(left.ToolName, right.ToolName, StringComparison.OrdinalIgnoreCase);
        });

        return usage;
    }

    private static IReadOnlyList<ProcessRuntimeToolUsageProjection> BuildToolUsage(
        ProcessRunRecord record)
    {
        var usage = (record.Facts?.RuntimeEventCategories ?? [])
            .OrderBy(category => category.Category)
            .Select(category => new ProcessRuntimeToolUsageProjection(
                GetRuntimeEventCategoryDisplayName(category.Category),
                category.EventCount,
                category.LastOccurredAtUtc,
                $"{category.EventCount.ToString(CultureInfo.InvariantCulture)} persisted " +
                $"{GetRuntimeEventCategoryDisplayName(category.Category).ToLowerInvariant()}, " +
                $"latest {category.LastOccurredAtUtc.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)}."))
            .ToList();
        var toolCallCount = record.Summary.Metrics.ToolCallCount;
        if (toolCallCount > 0)
        {
            usage.Add(new ProcessRuntimeToolUsageProjection(
                "Recorded tool calls",
                toolCallCount,
                record.Summary.Metrics.EndedAtUtc,
                $"{toolCallCount.ToString(CultureInfo.InvariantCulture)} persisted tool call(s) across the completed process run."));
        }

        return usage;
    }

    private static string GetRuntimeEventCategoryDisplayName(
        ProcessRunRuntimeEventCategory category)
        => category switch
        {
            ProcessRunRuntimeEventCategory.RunLifecycle => "Run lifecycle events",
            ProcessRunRuntimeEventCategory.Step => "Step events",
            ProcessRunRuntimeEventCategory.Dispatch => "Dispatch events",
            ProcessRunRuntimeEventCategory.Manager => "Manager events",
            ProcessRunRuntimeEventCategory.Other => "Other runtime events",
            _ => throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "Unsupported process runtime event category.")
        };

    private sealed class RuntimeMetricAccumulator(DateTimeOffset timestampUtc)
    {
        private DateTimeOffset firstEventAtUtc;
        private DateTimeOffset lastEventAtUtc;

        public DateTimeOffset TimestampUtc { get; } = timestampUtc;

        public int EventCount { get; private set; }

        public int ManagerEventCount { get; private set; }

        public int ToolCallCount { get; private set; }

        public int InputTokens { get; private set; }

        public int CachedInputTokens { get; private set; }

        public int OutputTokens { get; private set; }

        public int TotalTokens { get; private set; }

        public decimal EstimatedCost { get; private set; }

        public decimal ActualCost { get; private set; }

        public void Add(ProcessTimelineEventProjection runtimeEvent)
        {
            if (EventCount == 0 || runtimeEvent.OccurredAtUtc < firstEventAtUtc)
            {
                firstEventAtUtc = runtimeEvent.OccurredAtUtc;
            }

            if (EventCount == 0 || runtimeEvent.OccurredAtUtc > lastEventAtUtc)
            {
                lastEventAtUtc = runtimeEvent.OccurredAtUtc;
            }

            EventCount++;
            if (IsManagerEvent(runtimeEvent))
            {
                ManagerEventCount++;
            }

            if (IsToolUsageEvent(runtimeEvent))
            {
                ToolCallCount++;
            }
        }

        public void Add(ProcessRuntimeUsageObservation usageObservation)
        {
            InputTokens += usageObservation.InputTokens;
            CachedInputTokens += usageObservation.CachedInputTokens;
            OutputTokens += usageObservation.OutputTokens;
            TotalTokens += ResolveTotalTokens(usageObservation);
            EstimatedCost += usageObservation.EstimatedCostUsd;
            ActualCost += usageObservation.ActualCostUsd;
        }

        public ProcessRuntimeMetricPointProjection ToProjection()
        {
            var durationMs = EventCount < 2
                ? 0
                : checked((long)Math.Max(0, (lastEventAtUtc - firstEventAtUtc).TotalMilliseconds));

            return new ProcessRuntimeMetricPointProjection(
                TimestampUtc,
                EventCount,
                ManagerEventCount,
                ToolCallCount,
                durationMs,
                InputTokens,
                CachedInputTokens,
                OutputTokens,
                TotalTokens,
                EstimatedCost: decimal.Round(EstimatedCost, 6, MidpointRounding.AwayFromZero),
                ActualCost: decimal.Round(ActualCost, 6, MidpointRounding.AwayFromZero));
        }
    }

    private sealed class DurableRuntimeMetricAccumulator(DateTimeOffset timestampUtc)
    {
        private long toolCallCount;
        private long inputTokens;
        private long cachedInputTokens;
        private long outputTokens;
        private long totalTokens;
        private decimal estimatedCost;
        private decimal actualCost;

        public DateTimeOffset TimestampUtc { get; } = timestampUtc;

        public int EventCount { get; private set; }

        public int ManagerEventCount { get; private set; }

        public long DurationMilliseconds { get; private set; }

        public void Add(ProcessRunRuntimeEventMinuteBucket bucket)
        {
            EventCount = bucket.EventCount;
            ManagerEventCount = bucket.ManagerEventCount;
            DurationMilliseconds = bucket.DurationMilliseconds;
        }

        public void Add(ProcessRunStepFact step)
        {
            toolCallCount += Math.Max(0, step.ToolCallCount);
            inputTokens += Math.Max(0, step.InputTokenCount);
            cachedInputTokens += Math.Max(0, step.CachedInputTokenCount);
            outputTokens += Math.Max(0, step.OutputTokenCount);
            totalTokens += Math.Max(0, step.TotalTokenCount);
            estimatedCost += Math.Max(0, step.EstimatedCost);
            actualCost += Math.Max(0, step.ActualCost);
        }

        public ProcessRuntimeMetricPointProjection ToProjection()
        {
            return new ProcessRuntimeMetricPointProjection(
                TimestampUtc,
                EventCount,
                ManagerEventCount,
                SaturateToInt(toolCallCount),
                DurationMilliseconds,
                SaturateToInt(inputTokens),
                SaturateToInt(cachedInputTokens),
                SaturateToInt(outputTokens),
                SaturateToInt(totalTokens),
                decimal.Round(estimatedCost, 6, MidpointRounding.AwayFromZero),
                decimal.Round(actualCost, 6, MidpointRounding.AwayFromZero));
        }
    }

    private static int ResolveTotalTokens(ProcessRuntimeUsageObservation usageObservation)
    {
        return usageObservation.TotalTokens > 0
            ? usageObservation.TotalTokens
            : Math.Max(0, usageObservation.InputTokens) + Math.Max(0, usageObservation.OutputTokens);
    }

    private sealed class RuntimeToolUsageAccumulator(string toolName)
    {
        public string ToolName { get; } = toolName;

        public int CallCount { get; private set; }

        public DateTimeOffset LastUsedAtUtc { get; private set; }

        public void Add(ProcessTimelineEventProjection runtimeEvent)
        {
            CallCount++;
            if (CallCount == 1 || runtimeEvent.OccurredAtUtc > LastUsedAtUtc)
            {
                LastUsedAtUtc = runtimeEvent.OccurredAtUtc;
            }
        }

        public ProcessRuntimeToolUsageProjection ToProjection()
            => new(
                ToolName,
                CallCount,
                LastUsedAtUtc,
                BuildToolUsageSummary(ToolName, CallCount, LastUsedAtUtc));
    }

    private static IReadOnlyList<ProcessManagerMessageProjection> BuildManagerMessages(
        IReadOnlyList<ProcessTimelineEventProjection> events)
    {
        return events
            .Where(IsManagerEvent)
            .OrderByDescending(runtimeEvent => runtimeEvent.OccurredAtUtc)
            .Select(runtimeEvent => new ProcessManagerMessageProjection(
                runtimeEvent.EventId.ToString(),
                runtimeEvent.RootRunId,
                runtimeEvent.RunId,
                NormalizeEventType(runtimeEvent.EventType),
                BuildManagerMessageSummary(runtimeEvent),
                runtimeEvent.OccurredAtUtc,
                runtimeEvent.Sensitivity,
                runtimeEvent.RestrictedDiagnosticReference))
            .ToArray();
    }

    private static string BuildRuntimeSummary(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        IReadOnlyList<ProcessTimelineEventProjection> events)
    {
        if (runs.Count == 0)
        {
            return "No process runs match the selected runtime history window.";
        }

        var active = runs.Count(run => run.IsActive);
        var attention = runs.Count(run => run.Status == ProcessProjectedRunStatus.NeedsAttention);
        var latest = runs.Max(run => run.LastEventAtUtc).LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
        return $"{runs.Count.ToString(CultureInfo.InvariantCulture)} run(s), {active.ToString(CultureInfo.InvariantCulture)} active, {attention.ToString(CultureInfo.InvariantCulture)} needing attention, {events.Count.ToString(CultureInfo.InvariantCulture)} event(s) on this page. Latest event {latest}.";
    }

    private static string BuildRuntimeSummary(ProcessRunRecord record)
    {
        if (record.Summary.FactsStatus != ProcessRunFactsStatus.Completed)
        {
            return FormattableString.Invariant(
                $"{record.Summary.Disposition} run ended at {record.Summary.Metrics.EndedAtUtc:O}. {BuildFactsStageStatus(record.Summary)} Detailed historical metrics were not loaded.");
        }

        var metrics = record.Summary.Metrics;
        var hardFacts = FormattableString.Invariant(
            $"{record.Summary.Disposition} run with {metrics.CompletedStepCount}/{metrics.TotalStepCount} completed steps, {metrics.ExecutionCount} executions, {metrics.TotalTokenCount:N0} tokens, and {metrics.ActualCost:0.####} actual cost.");
        return record.Summary.Narrative is { } narrative
            ? $"{EnsureSentence(narrative.Overview)} {hardFacts}"
            : $"{hardFacts} Manager summary status: {record.Summary.NarrativeStatus}.";
    }

    private static string BuildAttentionSummary(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        IReadOnlyList<ProcessIncidentProjection> incidents,
        IReadOnlyList<ProcessTimelineEventProjection> events,
        ProcessRunDetailProjection? selectedRun,
        IReadOnlyList<ProcessRuntimeActiveAgentProjection> activeAgents)
    {
        if (selectedRun is not null)
        {
            var selectedLiveRun = runs.FirstOrDefault(run => run.RunId == selectedRun.RunId);
            if (selectedLiveRun is not null)
            {
                var selectedAction = selectedLiveRun.OperatorActions.FirstOrDefault(action => action.IsEnabled);
                if (selectedAction is not null)
                {
                    var actionSummary = string.IsNullOrWhiteSpace(selectedAction.ProblemSummary)
                        ? selectedAction.Summary
                        : selectedAction.ProblemSummary;
                    return $"Cause: {EnsureSentence(actionSummary)} Next action: {selectedAction.Label.ToLowerInvariant()} for {selectedAction.StepKey}.";
                }
            }

            var selectedIncident = incidents
                .Where(incident => incident.RunId == selectedRun.RunId)
                .OrderByDescending(incident => incident.RaisedAtUtc)
                .FirstOrDefault();
            if (selectedIncident is not null)
            {
                return $"Cause: {EnsureSentence(selectedIncident.SafeSummary)} Next action: inspect diagnostic reference {selectedIncident.DiagnosticReference}.";
            }

            var selectedAgentCount = activeAgents.Count(agent => agent.RunId == selectedRun.RunId.Value && agent.IsWorking);
            if (selectedAgentCount > 0)
            {
                return $"{selectedAgentCount.ToString(CultureInfo.InvariantCulture)} active agent(s) are working on run {selectedRun.RunId.Value.ToString("N")[..8]}; no operator action is currently required.";
            }

            if (selectedLiveRun?.CurrentStep is { } selectedCurrentStep)
            {
                return $"Current step: {EnsureSentence(selectedCurrentStep.Summary)} No operator action is currently required unless the lease expires or the step reports a blocker.";
            }

            var selectedChildWait = selectedLiveRun is null
                ? null
                : NormalizeChildRunWaits(selectedLiveRun).FirstOrDefault();
            if (selectedChildWait is not null)
            {
                return $"Waiting on child process: {EnsureSentence(selectedChildWait.Summary)} Open child run {selectedChildWait.ChildRunId.ToString("N")[..8]} for the current blocker or active step.";
            }

            return $"Run {selectedRun.RunId.Value.ToString("N")[..8]} has no current operator action in the selected history window.";
        }

        var incident = incidents.OrderByDescending(item => item.RaisedAtUtc).FirstOrDefault();
        if (incident is not null)
        {
            return $"Cause: {EnsureSentence(incident.SafeSummary)} Next action: inspect diagnostic reference {incident.DiagnosticReference}.";
        }

        var attentionRun = runs
            .Where(run => run.Status == ProcessProjectedRunStatus.NeedsAttention)
            .OrderByDescending(run => run.LastEventAtUtc)
            .FirstOrDefault();
        if (attentionRun is not null)
        {
            var causeEvent = ResolveAttentionCauseEvent(attentionRun.RecentEvents);
            var cause = causeEvent is null
                ? "Run needs operator attention."
                : BuildManagerMessageSummary(new ProcessTimelineEventProjection(
                    causeEvent.EventId,
                    causeEvent.GlobalSequence,
                    causeEvent.RootRunId,
                    causeEvent.RunId,
                    causeEvent.EventType,
                    causeEvent.OccurredAtUtc,
                    causeEvent.Sensitivity,
                    causeEvent.Summary,
                    causeEvent.RestrictedDiagnosticReference));
            var action = attentionRun.OperatorActions.FirstOrDefault(action => action.IsEnabled);
            var nextAction = action is null
                ? "open the selected run and review manager messages."
                : $"{action.Label.ToLowerInvariant()} for {action.StepKey}.";
            return $"Cause: {EnsureSentence(cause)} Next action: {nextAction}";
        }

        var childWaitRun = runs
            .Where(run => NormalizeChildRunWaits(run).Count > 0)
            .OrderByDescending(run => run.LastEventAtUtc)
            .FirstOrDefault();
        if (childWaitRun is not null)
        {
            var childWait = NormalizeChildRunWaits(childWaitRun)[0];
            return $"Waiting on child process: {EnsureSentence(childWait.Summary)} Open child run {childWait.ChildRunId.ToString("N")[..8]} for the current blocker or active step.";
        }

        var activeCurrentStepRun = runs
            .Where(run => run.CurrentStep?.IsWorking == true)
            .OrderByDescending(run => run.CurrentStep!.UpdatedAtUtc)
            .FirstOrDefault();
        if (activeCurrentStepRun?.CurrentStep is { } activeCurrentStep)
        {
            return $"Active step: {EnsureSentence(activeCurrentStep.Summary)} No operator action is currently required unless the lease expires or the step reports a blocker.";
        }

        var latestAttentionEvent = events
            .Where(runtimeEvent => IsAttentionEventType(runtimeEvent.EventType))
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefault();
        if (latestAttentionEvent is not null)
        {
            return $"Latest attention signal: {EnsureSentence(BuildManagerMessageSummary(latestAttentionEvent))}";
        }

        var latestManagerEvent = events.Where(IsManagerEvent).OrderByDescending(item => item.OccurredAtUtc).FirstOrDefault();
        if (latestManagerEvent is not null)
        {
            return $"Latest manager signal: {EnsureSentence(BuildManagerMessageSummary(latestManagerEvent))}";
        }

        return "No blocked or manager-escalated process runs are present in the selected history window.";
    }

    private static string BuildAttentionSummary(ProcessRunRecord record)
    {
        if (record.Summary.FactsStatus != ProcessRunFactsStatus.Completed || record.Facts is null)
        {
            return BuildFactsStageStatus(record.Summary);
        }

        if (record.Summary.Narrative is { } narrative)
        {
            var problem = narrative.Problems.FirstOrDefault();
            var followUp = narrative.FollowUps.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(problem))
            {
                return string.IsNullOrWhiteSpace(followUp)
                    ? $"Recorded problem: {EnsureSentence(problem)}"
                    : $"Recorded problem: {EnsureSentence(problem)} Follow-up: {EnsureSentence(followUp)}";
            }

            return $"Recorded outcome: {EnsureSentence(narrative.Outcome)}";
        }

        if (record.Summary.CompletenessWarnings.FirstOrDefault() is { } warning)
        {
            return $"Durable record is {record.Summary.Completeness.ToString().ToLowerInvariant()}: {EnsureSentence(warning.ToString())}";
        }

        return $"Durable record facts are {record.Summary.FactsStatus.ToString().ToLowerInvariant()}; manager summary is {record.Summary.NarrativeStatus.ToString().ToLowerInvariant()}.";
    }

    private static string BuildFactsStageStatus(ProcessRunRecordSummary summary)
    {
        var retry = summary.FactsNextAttemptAtUtc is { } nextAttemptAtUtc
            ? $" Next retry: {nextAttemptAtUtc:O}."
            : summary.FactsStatus == ProcessRunFactsStatus.Failed
                ? " No automatic retry remains."
                : string.Empty;
        var failure = string.IsNullOrWhiteSpace(summary.FactsLastErrorClass)
            ? string.Empty
            : $" Last error: {summary.FactsLastErrorClass}; diagnostic reference: {summary.FactsLastErrorDiagnosticReference ?? "unavailable"}.";
        return FormattableString.Invariant(
            $"Durable facts are {summary.FactsStatus.ToString().ToLowerInvariant()} after {summary.FactsAttemptCount} attempt(s).{retry}{failure}");
    }

    private static string BuildManagerMessageSummary(ProcessTimelineEventProjection runtimeEvent)
        => runtimeEvent.Sensitivity == ProcessProjectedSensitivity.Restricted
            ? $"Restricted manager event {runtimeEvent.EventType}."
            : runtimeEvent.EventType switch
            {
                ProcessRuntimeProjectionEventTypeNames.ManagerIncidentRaised => "Manager incident raised; operator review is required.",
                ProcessRuntimeProjectionEventTypeNames.ManagerRecoveryDenied => "Manager recovery was denied by policy.",
                ProcessRuntimeProjectionEventTypeNames.ManagerBranchDecisionRejected => "Manager branch decision was rejected.",
                ProcessRuntimeProjectionEventTypeNames.ManagerLoopBudgetEscalated => "Manager loop budget escalated.",
                _ when runtimeEvent.EventType.StartsWith("Manager", StringComparison.Ordinal) => NormalizeEventType(runtimeEvent.EventType),
                _ => runtimeEvent.Summary
            };

    private static IReadOnlyList<ProcessRuntimeChildRunWaitProjection> NormalizeChildRunWaits(ProcessLiveProcessSnapshot run)
        => run.WaitingOnChildRuns ?? [];

    private static string BuildToolUsageSummary(string toolName, int count, DateTimeOffset lastUsedAtUtc)
        => $"{count.ToString(CultureInfo.InvariantCulture)} event(s), latest {lastUsedAtUtc.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)}.";

    private static bool IsManagerEvent(ProcessTimelineEventProjection runtimeEvent)
        => runtimeEvent.EventType.StartsWith("Manager", StringComparison.Ordinal);

    private static int SaturateToInt(long value)
        => value switch
        {
            > int.MaxValue => int.MaxValue,
            < int.MinValue => int.MinValue,
            _ => (int)value
        };

    private static bool IsToolUsageEvent(ProcessTimelineEventProjection runtimeEvent)
        => runtimeEvent.EventType.StartsWith("Dispatch", StringComparison.Ordinal) ||
           runtimeEvent.EventType.StartsWith("Step", StringComparison.Ordinal) ||
           runtimeEvent.EventType.StartsWith("Manager", StringComparison.Ordinal);

    private static ProcessLiveRunEventProjection? ResolveAttentionCauseEvent(IReadOnlyList<ProcessLiveRunEventProjection> events)
        => events
            .OrderByDescending(item => IsAttentionEventType(item.EventType))
            .ThenByDescending(item => item.OccurredAtUtc)
            .FirstOrDefault();

    private static bool IsAttentionEventType(string eventType)
        => eventType.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Denied", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Rejected", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Escalated", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Incident", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset TruncateToMinute(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            second: 0,
            TimeSpan.Zero);
    }

    private static string NormalizeEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return "Runtime event";
        }

        var words = new List<string>();
        var start = 0;
        for (var index = 1; index < eventType.Length; index++)
        {
            if (!char.IsUpper(eventType[index]))
            {
                continue;
            }

            words.Add(eventType[start..index]);
            start = index;
        }

        words.Add(eventType[start..]);
        return string.Join(' ', words);
    }

    private static string EnsureSentence(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.EndsWith(".", StringComparison.Ordinal) || trimmed.EndsWith("!", StringComparison.Ordinal) || trimmed.EndsWith("?", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return $"{trimmed}.";
    }

    private static ProcessWorkspaceProjectionRefreshProjection CreateRefreshProjection(
        bool forceRefresh,
        DateTimeOffset observedAtUtc,
        ProcessProjectionFreshness? freshness)
        => new(
            freshness is null && forceRefresh
                ? ProcessWorkspaceProjectionStatus.RefreshRequested
                : freshness is null
                    ? ProcessWorkspaceProjectionStatus.Ready
                    : ProcessWorkspaceProjectionStatus.Ready,
            observedAtUtc,
            freshness?.SourceGlobalSequence ?? 0,
            freshness?.Lag.BacklogEventCount ?? 0,
            freshness is null
                ? "Runtime projection has no events in the current window."
                : $"Runtime projection processed sequence {freshness.SourceGlobalSequence.ToString(CultureInfo.InvariantCulture)} with {freshness.Lag.BacklogEventCount.ToString(CultureInfo.InvariantCulture)} backlog event(s).");

    private static IReadOnlyList<ProcessWorkspaceTabProjection> CreateTabs(
        ProcessDefinitionCatalogProjection definitionCatalog,
        ProcessRuntimeWorkspaceProjection runtime)
    {
        var definitionCount = definitionCatalog.PublishedDefinitionCount + definitionCatalog.DraftDefinitionCount;
        var definitionCountText = definitionCount.ToString(CultureInfo.InvariantCulture);
        var activeRunCountText = runtime.Runs.Count(run => run.IsActive).ToString(CultureInfo.InvariantCulture);
        var historyCountText = runtime.Events.Count.ToString(CultureInfo.InvariantCulture);

        return
        [
            new(
                ProcessWorkspaceTabKey.Definitions,
                "Definitions",
                "account_tree",
                "Definition catalog, template compatibility, and selected definition context.",
                definitionCountText,
                IsEnabled: true),
            new(
                ProcessWorkspaceTabKey.LaunchPlans,
                "Launch plans",
                "rocket_launch",
                "Launch planning entry point reserved for application commands.",
                "0",
                IsEnabled: true),
            new(
                ProcessWorkspaceTabKey.LiveRuns,
                "Live runs",
                "monitor_heart",
                "Live runtime projection surface.",
                activeRunCountText,
                IsEnabled: true),
            new(
                ProcessWorkspaceTabKey.History,
                "History",
                "history",
                "Read-only runtime history and legacy archive context.",
                historyCountText,
                IsEnabled: true)
        ];
    }

    private static IReadOnlyList<ProcessWorkspaceCommandProjection> CreateCommands(
        ProcessWorkspaceAuthorizationProjection authorization)
        =>
        [
            new(
                ProcessWorkspaceCommandKind.RefreshProjections,
                "Refresh",
                "refresh",
                authorization.CanRefreshProjections,
                authorization.CanRefreshProjections ? null : "Projection refresh is not authorized."),
            new(
                ProcessWorkspaceCommandKind.OpenAgentContext,
                "Agent context",
                "smart_toy",
                authorization.CanOpenAgentContext,
                authorization.CanOpenAgentContext ? null : "Agent context is not authorized."),
            new(
                ProcessWorkspaceCommandKind.CreateDefinition,
                "New definition",
                "add",
                authorization.CanEditDefinitions,
                "Definition editing is not available in this workspace shell."),
            new(
                ProcessWorkspaceCommandKind.FeedDefaults,
                "Feed defaults",
                "download",
                authorization.CanRefreshProjections,
                authorization.CanRefreshProjections ? null : "Projection refresh is not authorized."),
            new(
                ProcessWorkspaceCommandKind.LaunchRun,
                "Launch",
                "rocket_launch",
                authorization.CanLaunchRuns,
                authorization.CanLaunchRuns ? null : "Runtime launch commands are not available in this workspace shell."),
            new(
                ProcessWorkspaceCommandKind.OpenLiveDashboard,
                "Live dashboard",
                "open_in_new",
                IsEnabled: true,
                DisabledReason: null)
        ];

    private static ProcessWorkspaceAgentEntryProjection CreateAgentEntry(
        ProcessWorkspaceShellScope scope,
        ProcessWorkspaceSelectionProjection selection,
        ProcessWorkspaceAuthorizationProjection authorization)
    {
        if (!authorization.CanOpenAgentContext)
        {
            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.WorkspaceContext,
                IsAvailable: false,
                "Agent context",
                WorkspaceContextPrefix,
                "Agent context is not authorized.");
        }

        if (selection.RunId.HasValue)
        {
            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.RunContext,
                IsAvailable: true,
                "Open run agent context",
                BuildContextKey(scope, RunContextSegment, selection.RunId.Value),
                DisabledReason: null);
        }

        if (selection.LaunchPlanId.HasValue)
        {
            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.LaunchPlanContext,
                IsAvailable: true,
                "Open launch-plan agent context",
                BuildContextKey(scope, LaunchPlanContextSegment, selection.LaunchPlanId.Value),
                DisabledReason: null);
        }

        if (scope.Kind == ProcessWorkspaceScopeKind.Project)
        {
            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.ProjectContext,
                IsAvailable: true,
                "Open project process agent context",
                $"{ProjectContextPrefix}:{scope.ProjectId:N}",
                DisabledReason: null);
        }

        return new ProcessWorkspaceAgentEntryProjection(
            ProcessWorkspaceAgentEntryKind.WorkspaceContext,
            IsAvailable: true,
            "Open process agent context",
            WorkspaceContextPrefix,
            DisabledReason: null);
    }

    private static string BuildContextKey(
        ProcessWorkspaceShellScope scope,
        string segment,
        Guid id)
    {
        var scopeKey = scope.Kind == ProcessWorkspaceScopeKind.Project
            ? $"{ProjectContextPrefix}:{scope.ProjectId:N}"
            : WorkspaceContextPrefix;

        return $"{scopeKey}:{segment}:{id:N}";
    }
}
