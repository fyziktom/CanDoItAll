using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessObservationService(
    ProcessesService processesService,
    ProcessWorkspaceRunDetailsLoader runDetailsLoader,
    IProcessEscalationService escalationService,
    ProcessRuntimeStateOverviewService runtimeStateOverviewService,
    IAgentFrameworkWorkspaceService workspaceService,
    ProcessObservationCache cache,
    IOptions<ProcessObservationCacheOptions> options,
    ILogger<ProcessObservationService> logger) : IProcessObservationService
{
    private const int MaxTimelineTake = 200;
    private const int LiveToolUsageLimit = 30;
    private const int LiveProcessOptionLimit = 250;
    private const int LiveEscalationLimit = 60;
    private const int LiveRunEventLimit = 60;

    public async Task<ProcessDashboardObservationSnapshot> GetDashboardSnapshotAsync(
        ProcessObservationDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedDefinitionIds = query.GetNormalizedDefinitionIds();
        if (normalizedDefinitionIds.Count == 0)
        {
            var observedAtUtc = DateTimeOffset.UtcNow;
            var revision = ProcessObservationSnapshotRevision.Create(observedAtUtc);
            return ProcessDashboardObservationSnapshot.Empty(
                query.ProjectId,
                revision,
                new ProcessObservationStaleness(
                    ProcessObservationFreshness.Fresh,
                    observedAtUtc,
                    observedAtUtc.Add(options.Value.GetInactiveDashboardAbsoluteExpiration())));
        }

        var cacheKey = new ProcessObservationCacheKey(
            ProcessObservationCacheKind.Dashboard,
            query.ProjectId,
            query.SelectedDefinitionId,
            RunId: null,
            StepRunId: null,
            ProcessObservationDefinitionSetKey.From(normalizedDefinitionIds),
            QueryFingerprint: BuildDashboardFingerprint(query));
        var policy = new ProcessObservationCachePolicy(
            query.IncludeActiveRunSummaries
                ? options.Value.GetActiveDashboardAbsoluteExpiration()
                : options.Value.GetInactiveDashboardAbsoluteExpiration(),
            options.Value.GetSlidingExpiration(),
            options.Value.DashboardEntrySize);
        var result = await cache.GetOrCreateAsync(
            cacheKey,
            policy,
            async token => await BuildDashboardSnapshotAsync(query, normalizedDefinitionIds, policy, token),
            query.ForceRefresh,
            cancellationToken);

        return result.Value with
        {
            Staleness = BuildStaleness(result)
        };
    }

    public async Task<ProcessLiveObservationSnapshot> GetLiveSnapshotAsync(
        ProcessLiveObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedQuery = query with
        {
            HistoryWindow = NormalizeHistoryWindow(query.HistoryWindow)
        };
        var cacheKey = new ProcessObservationCacheKey(
            ProcessObservationCacheKind.LiveSnapshot,
            normalizedQuery.ProjectId,
            DefinitionId: null,
            normalizedQuery.ProcessRunId,
            StepRunId: null,
            ProcessObservationDefinitionSetKey.From([]),
            QueryFingerprint: $"live:{normalizedQuery.HistoryWindow}");
        var policy = new ProcessObservationCachePolicy(
            options.Value.GetActiveDashboardAbsoluteExpiration(),
            options.Value.GetSlidingExpiration(),
            options.Value.DashboardEntrySize);
        var result = await cache.GetOrCreateAsync(
            cacheKey,
            policy,
            async token => await BuildLiveSnapshotAsync(normalizedQuery, policy, token),
            normalizedQuery.ForceRefresh,
            cancellationToken);

        return result.Value with
        {
            Staleness = BuildStaleness(result)
        };
    }

    public async Task<ProcessRunObservationSnapshot> GetRunSnapshotAsync(
        ProcessRunObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id is required.", nameof(query));
        }

        var cacheKey = new ProcessObservationCacheKey(
            ProcessObservationCacheKind.RunSnapshot,
            query.ProjectId,
            DefinitionId: null,
            query.RunId,
            StepRunId: null,
            ProcessObservationDefinitionSetKey.From([]),
            QueryFingerprint: "run-details");
        var policy = new ProcessObservationCachePolicy(
            options.Value.GetRunDetailsAbsoluteExpiration(),
            options.Value.GetSlidingExpiration(),
            options.Value.RunDetailsEntrySize);
        var result = await cache.GetOrCreateAsync(
            cacheKey,
            policy,
            async token =>
            {
                var observedAtUtc = DateTimeOffset.UtcNow;
                var details = await runDetailsLoader.LoadAsync(query.RunId, token);
                return new ProcessRunObservationSnapshot(
                    query.RunId,
                    details,
                    ProcessObservationSnapshotRevision.Create(observedAtUtc),
                    new ProcessObservationStaleness(
                        ProcessObservationFreshness.Fresh,
                        observedAtUtc,
                        observedAtUtc.Add(policy.AbsoluteExpiration)));
            },
            query.ForceRefresh,
            cancellationToken);

        return result.Value with
        {
            Staleness = BuildStaleness(result)
        };
    }

    public async Task<ProcessStageObservationSnapshot> GetStageSnapshotAsync(
        ProcessStageObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id is required.", nameof(query));
        }

        if (query.StepRunId == Guid.Empty)
        {
            throw new ArgumentException("Step run id is required.", nameof(query));
        }

        var runSnapshot = await GetRunSnapshotAsync(
            new ProcessRunObservationQuery(query.RunId, query.ProjectId, query.ForceRefresh),
            cancellationToken);
        var stage = runSnapshot.Details.StepRuns.FirstOrDefault(item => item.Id == query.StepRunId);
        var timeline = ToTimelineItems(runSnapshot.Details.AttemptTimeline, query.StepRunId);

        return new ProcessStageObservationSnapshot(
            query.RunId,
            query.StepRunId,
            stage,
            timeline,
            runSnapshot.Revision,
            runSnapshot.Staleness);
    }

    public async Task<ProcessObservationTimelinePage> GetTimelinePageAsync(
        ProcessObservationTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id is required.", nameof(query));
        }

        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take, 1, MaxTimelineTake);
        var cacheKey = new ProcessObservationCacheKey(
            ProcessObservationCacheKind.TimelinePage,
            query.ProjectId,
            DefinitionId: null,
            query.RunId,
            query.StepRunId,
            ProcessObservationDefinitionSetKey.From([]),
            QueryFingerprint: $"{skip}:{take}");
        var policy = new ProcessObservationCachePolicy(
            options.Value.GetTimelineAbsoluteExpiration(),
            options.Value.GetSlidingExpiration(),
            options.Value.TimelineEntrySize);
        var result = await cache.GetOrCreateAsync(
            cacheKey,
            policy,
            async token =>
            {
                var runSnapshot = await GetRunSnapshotAsync(
                    new ProcessRunObservationQuery(query.RunId, query.ProjectId, query.ForceRefresh),
                    token);
                var allItems = ToTimelineItems(runSnapshot.Details.AttemptTimeline, query.StepRunId);
                return new ProcessObservationTimelinePage(
                    query.RunId,
                    query.StepRunId,
                    skip,
                    take,
                    allItems.Count,
                    PageItems(allItems, skip, take),
                    runSnapshot.Revision,
                    runSnapshot.Staleness);
            },
            query.ForceRefresh,
            cancellationToken);

        return result.Value with
        {
            Staleness = BuildStaleness(result)
        };
    }

    public async Task<ProcessObservationDialogPayload> GetDialogPayloadAsync(
        ProcessObservationDialogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var descriptor = query.Descriptor;
        if (!descriptor.ProcessRunId.HasValue)
        {
            throw new ArgumentException("Dialog descriptor must include a process run id.", nameof(query));
        }

        logger.LogDebug(
            "Loading process observation dialog payload. Kind={DialogKind} RunId={RunId} StepRunId={StepRunId}.",
            descriptor.Kind,
            descriptor.ProcessRunId,
            descriptor.StepRunId);

        var runId = descriptor.ProcessRunId.Value;
        ProcessRunObservationSnapshot? runSnapshot = null;
        ProcessStageObservationSnapshot? stageSnapshot = null;
        ProcessObservationTimelinePage? timelinePage = null;

        switch (descriptor.Kind)
        {
            case ProcessObservationDialogKind.StageDetails:
                if (!descriptor.StepRunId.HasValue)
                {
                    throw new ArgumentException("Stage detail dialogs must include a step run id.", nameof(query));
                }

                stageSnapshot = await GetStageSnapshotAsync(
                    new ProcessStageObservationQuery(runId, descriptor.StepRunId.Value, query.ProjectId, query.ForceRefresh),
                    cancellationToken);
                break;
            case ProcessObservationDialogKind.Timeline:
                timelinePage = await GetTimelinePageAsync(
                    new ProcessObservationTimelineQuery(runId, descriptor.StepRunId, ProjectId: query.ProjectId, ForceRefresh: query.ForceRefresh),
                    cancellationToken);
                break;
            default:
                runSnapshot = await GetRunSnapshotAsync(
                    new ProcessRunObservationQuery(runId, query.ProjectId, query.ForceRefresh),
                    cancellationToken);
                break;
        }

        var revision = runSnapshot?.Revision ?? stageSnapshot?.Revision ?? timelinePage?.Revision ??
            ProcessObservationSnapshotRevision.Create(DateTimeOffset.UtcNow);
        var staleness = runSnapshot?.Staleness ?? stageSnapshot?.Staleness ?? timelinePage?.Staleness ??
            new ProcessObservationStaleness(
                ProcessObservationFreshness.Fresh,
                revision.ObservedAtUtc,
                revision.ObservedAtUtc.Add(options.Value.GetRunDetailsAbsoluteExpiration()));

        return new ProcessObservationDialogPayload(
            descriptor,
            runSnapshot,
            stageSnapshot,
            timelinePage,
            revision,
            staleness);
    }

    private async Task<ProcessDashboardObservationSnapshot> BuildDashboardSnapshotAsync(
        ProcessObservationDashboardQuery query,
        IReadOnlyList<Guid> normalizedDefinitionIds,
        ProcessObservationCachePolicy policy,
        CancellationToken cancellationToken)
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var runtimeOverview = await runtimeStateOverviewService.GetOverviewAsync(
            normalizedDefinitionIds,
            query.ProjectId,
            forceRefresh: true,
            cancellationToken);
        IReadOnlyList<ProcessRunListItem> runs = [];
        IReadOnlyList<ProcessActiveRunSummaryViewModel> activeRunSummaries = [];
        ProcessAnalyticsSummary? analytics = null;

        if (query.SelectedDefinitionId.HasValue && query.IncludeRuns)
        {
            runs = await processesService.ListRunsAsync(
                query.SelectedDefinitionId,
                query.ProjectId,
                cancellationToken);
            if (query.IncludeActiveRunSummaries)
            {
                activeRunSummaries = await runDetailsLoader.LoadActiveRunSummariesAsync(runs, cancellationToken);
            }
        }

        if (query.IncludeAnalytics)
        {
            analytics = await processesService.GetAnalyticsAsync(
                query.SelectedDefinitionId,
                query.ProjectId,
                cancellationToken);
        }

        var sourceMaxUpdatedAtUtc = ResolveSourceMaxUpdatedAtUtc(runs, activeRunSummaries);
        var revision = ProcessObservationSnapshotRevision.Create(observedAtUtc, sourceMaxUpdatedAtUtc);
        return new ProcessDashboardObservationSnapshot(
            query.ProjectId,
            runtimeOverview,
            runs,
            activeRunSummaries,
            analytics,
            BuildDialogDescriptors(runs),
            revision,
            new ProcessObservationStaleness(
                ProcessObservationFreshness.Fresh,
                observedAtUtc,
                observedAtUtc.Add(policy.AbsoluteExpiration)));
    }

    private async Task<ProcessLiveObservationSnapshot> BuildLiveSnapshotAsync(
        ProcessLiveObservationQuery query,
        ProcessObservationCachePolicy policy,
        CancellationToken cancellationToken)
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var definitions = await processesService.ListDefinitionsAsync(query.ProjectId, cancellationToken);
        var definitionsById = definitions.ToDictionary(item => item.Id);
        var allRuns = await processesService.ListRunsAsync(
            definitionId: null,
            query.ProjectId,
            cancellationToken);
        var observedRuns = allRuns
            .Where(run => ShouldIncludeLiveRun(run, query.ProcessRunId))
            .ToList();
        var observedRunIds = observedRuns
            .Select(item => item.Id)
            .ToArray();
        var activeRunSummaries = await runDetailsLoader.LoadActiveRunSummariesAsync(
            observedRuns,
            cancellationToken);
        var activeRunSummariesByRunId = activeRunSummaries.ToDictionary(item => item.RunId);
        var historyStartUtc = observedAtUtc.Subtract(ResolveHistoryWindowSpan(query.HistoryWindow));
        var liveEscalationsByRunId = await escalationService.ListForRunsAsync(observedRunIds, cancellationToken);
        var escalationCards = BuildLiveEscalationCards(
            observedRuns,
            definitionsById,
            liveEscalationsByRunId);
        var runEventCards = BuildLiveRunEventCards(
            allRuns,
            definitionsById,
            historyStartUtc,
            query.ProcessRunId);
        HashSet<Guid> processRunIdsForHistory = query.ProcessRunId.HasValue
            ? [query.ProcessRunId.Value]
            : allRuns.Select(item => item.Id).ToHashSet();
        var executionRuns = await LoadLiveExecutionRunsAsync(
            processRunIdsForHistory,
            query.HistoryWindow,
            historyStartUtc,
            cancellationToken);
        var executionRunDetails = await LoadLiveExecutionRunDetailsAsync(
            executionRuns,
            query.HistoryWindow,
            cancellationToken);
        var metrics = ExtractLiveMetrics(executionRunDetails, historyStartUtc);
        var toolReceipts = ExtractLiveToolReceipts(executionRunDetails, historyStartUtc);
        var sourceMaxUpdatedAtUtc = ResolveLiveSourceMaxUpdatedAtUtc(
            observedRuns,
            activeRunSummaries,
            escalationCards,
            runEventCards,
            executionRuns,
            metrics,
            toolReceipts);
        var revision = ProcessObservationSnapshotRevision.Create(observedAtUtc, sourceMaxUpdatedAtUtc);

        return new ProcessLiveObservationSnapshot(
            query.ProjectId,
            query.HistoryWindow,
            query.ProcessRunId,
            BuildLiveProcessOptions(allRuns, definitionsById),
            BuildLiveRunCards(observedRuns, definitionsById, activeRunSummariesByRunId),
            escalationCards,
            runEventCards,
            BuildLiveAgentCards(activeRunSummaries),
            BuildLiveStats(observedRuns, activeRunSummaries, metrics),
            BuildLiveMetricPoints(metrics, query.HistoryWindow),
            BuildLiveToolUsage(toolReceipts),
            revision,
            new ProcessObservationStaleness(
                ProcessObservationFreshness.Fresh,
                observedAtUtc,
                observedAtUtc.Add(policy.AbsoluteExpiration)));
    }

    private async Task<IReadOnlyList<ExecutionRunRecord>> LoadLiveExecutionRunsAsync(
        IReadOnlySet<Guid> processRunIds,
        ProcessLiveHistoryWindow historyWindow,
        DateTimeOffset historyStartUtc,
        CancellationToken cancellationToken)
    {
        if (processRunIds.Count == 0)
        {
            return [];
        }

        return (await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    Take: ResolveLiveExecutionRunTake(historyWindow),
                    UpdatedFromUtc: historyStartUtc),
                cancellationToken))
            .Select(item => new LiveExecutionRunMatch(TryParseGuid(item.ProcessRunId), item))
            .Where(item => item.ProcessRunId.HasValue && processRunIds.Contains(item.ProcessRunId.Value))
            .Select(item => item.ExecutionRun)
            .OrderBy(item => item.UpdatedAtUtc)
            .ToArray();
    }

    private async Task<IReadOnlyList<ExecutionRunDetail>> LoadLiveExecutionRunDetailsAsync(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        ProcessLiveHistoryWindow historyWindow,
        CancellationToken cancellationToken)
    {
        if (executionRuns.Count == 0)
        {
            return [];
        }

        var details = new List<ExecutionRunDetail>();
        foreach (var executionRun in executionRuns
                     .OrderByDescending(item => item.UpdatedAtUtc)
                     .Take(ResolveLiveExecutionRunDetailTake(historyWindow)))
        {
            try
            {
                details.Add(await workspaceService.GetExecutionRunDetailAsync(
                    executionRun.Id,
                    cancellationToken));
            }
            catch (InvalidOperationException exception)
            {
                logger.LogDebug(
                    exception,
                    "Skipped live process execution detail load because the execution run was not available. ExecutionRunId={ExecutionRunId}",
                    executionRun.Id);
            }
        }

        return details;
    }

    private static IReadOnlyList<AgentRunMetric> ExtractLiveMetrics(
        IReadOnlyList<ExecutionRunDetail> executionRunDetails,
        DateTimeOffset historyStartUtc)
    {
        return executionRunDetails
            .SelectMany(item => item.Metrics)
            .Where(item => item.CreatedAtUtc >= historyStartUtc)
            .OrderBy(item => item.CreatedAtUtc)
            .ToArray();
    }

    private static IReadOnlyList<ToolExecutionReceiptRecord> ExtractLiveToolReceipts(
        IReadOnlyList<ExecutionRunDetail> executionRunDetails,
        DateTimeOffset historyStartUtc)
    {
        return executionRunDetails
            .SelectMany(item => item.ToolReceipts)
            .Where(item =>
                item.StartedAtUtc >= historyStartUtc ||
                item.CompletedAtUtc >= historyStartUtc)
            .OrderBy(item => item.StartedAtUtc)
            .ToArray();
    }

    private static IReadOnlyList<ProcessLiveProcessOption> BuildLiveProcessOptions(
        IReadOnlyList<ProcessRunListItem> runs,
        IReadOnlyDictionary<Guid, ProcessDefinitionListItem> definitionsById)
    {
        return runs
            .OrderBy(item => IsLiveObservedRunStatus(item.Status) ? 0 : 1)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .Take(LiveProcessOptionLimit)
            .Select(item => new ProcessLiveProcessOption(
                item.Id,
                item.Name,
                ResolveDefinitionName(item.ProcessDefinitionId, definitionsById),
                item.Status,
                item.UpdatedAtUtc))
            .ToArray();
    }

    private static IReadOnlyList<ProcessLiveRunCard> BuildLiveRunCards(
        IReadOnlyList<ProcessRunListItem> runs,
        IReadOnlyDictionary<Guid, ProcessDefinitionListItem> definitionsById,
        IReadOnlyDictionary<Guid, ProcessActiveRunSummaryViewModel> activeRunSummariesByRunId)
    {
        return runs
            .Select(run =>
            {
                activeRunSummariesByRunId.TryGetValue(run.Id, out var activeSummary);
                return new ProcessLiveRunCard(
                    run.Id,
                    run.ProcessDefinitionId,
                    ResolveDefinitionName(run.ProcessDefinitionId, definitionsById),
                    run.Name,
                    run.Status,
                    run.UpdatedAtUtc,
                    run.CompletedStepCount,
                    run.TotalStepCount,
                    run.BlockedStepCount,
                    run.CapabilityGapCount,
                    run.EstimatedCost,
                    run.ActualCost,
                    activeSummary?.ActiveExecutionCount ?? 0,
                    activeSummary?.PendingApprovalCount ?? 0,
                    activeSummary?.PendingOutboxCount ?? 0,
                    activeSummary?.DeadLetteredOutboxCount ?? 0,
                    activeSummary?.BlockedOrFailedStepCount ?? run.BlockedStepCount,
                    run.ManagerAgentId,
                    run.ManagerAgentName,
                    activeSummary?.HealthSummary ?? string.Empty);
            })
            .OrderBy(item => ResolveLiveRunSortRank(item.Status))
            .ThenByDescending(item => item.ActiveExecutionCount)
            .ThenByDescending(item => item.PendingApprovalCount)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.RunName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ProcessLiveEscalationCard> BuildLiveEscalationCards(
        IReadOnlyList<ProcessRunListItem> runs,
        IReadOnlyDictionary<Guid, ProcessDefinitionListItem> definitionsById,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessEscalationViewModel>> escalationsByRunId)
    {
        if (runs.Count == 0 || escalationsByRunId.Count == 0)
        {
            return [];
        }

        var cards = new List<ProcessLiveEscalationCard>();
        foreach (var run in runs)
        {
            if (!escalationsByRunId.TryGetValue(run.Id, out var escalations))
            {
                continue;
            }

            foreach (var escalation in escalations.Where(item => item.IsOpen))
            {
                cards.Add(new ProcessLiveEscalationCard(
                    BuildLiveEscalationKey(escalation),
                    run.Id,
                    run.ProcessDefinitionId,
                    ResolveDefinitionName(run.ProcessDefinitionId, definitionsById),
                    run.Name,
                    run.Status,
                    escalation.Id,
                    escalation.StepRunId,
                    escalation.StepTitle,
                    escalation.Kind,
                    escalation.Severity,
                    escalation.Status,
                    escalation.Title,
                    escalation.Reason,
                    escalation.Owner,
                    escalation.SourceExecutionRunId,
                    escalation.SourceApprovalId,
                    escalation.SourceToolName,
                    escalation.CreatedAtUtc,
                    escalation.UpdatedAtUtc,
                    escalation.DueAtUtc,
                    run.ManagerAgentId,
                    run.ManagerAgentName));
            }
        }

        return cards
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.RunName, StringComparer.OrdinalIgnoreCase)
            .Take(LiveEscalationLimit)
            .ToArray();
    }

    private static IReadOnlyList<ProcessLiveRunEventCard> BuildLiveRunEventCards(
        IReadOnlyList<ProcessRunListItem> runs,
        IReadOnlyDictionary<Guid, ProcessDefinitionListItem> definitionsById,
        DateTimeOffset historyStartUtc,
        Guid? processRunId)
    {
        return runs
            .Where(run => IsLiveRunEventStatus(run.Status))
            .Where(run => processRunId.HasValue
                ? run.Id == processRunId.Value
                : run.UpdatedAtUtc >= historyStartUtc)
            .Select(run => new ProcessLiveRunEventCard(
                BuildLiveRunEventKey(run),
                run.Id,
                run.ProcessDefinitionId,
                ResolveDefinitionName(run.ProcessDefinitionId, definitionsById),
                run.Name,
                run.Status,
                ResolveLiveRunEventTitle(run),
                ResolveLiveRunEventSummary(run),
                ResolveLiveRunEventIcon(run.Status),
                run.UpdatedAtUtc,
                run.ManagerAgentId,
                run.ManagerAgentName))
            .OrderBy(item => ResolveLiveRunEventSortRank(item.Status))
            .ThenByDescending(item => item.OccurredAtUtc)
            .ThenBy(item => item.RunName, StringComparer.OrdinalIgnoreCase)
            .Take(LiveRunEventLimit)
            .ToArray();
    }

    private static IReadOnlyList<ProcessLiveAgentCard> BuildLiveAgentCards(
        IReadOnlyList<ProcessActiveRunSummaryViewModel> activeRunSummaries)
    {
        return activeRunSummaries
            .SelectMany(summary => summary.Agents.Select(agent => new ProcessLiveAgentCard(
                summary.RunId,
                summary.RunName,
                agent.ExecutionRunId,
                agent.AgentId,
                agent.AgentName,
                agent.AgentRoleTitle,
                agent.StepTitle,
                agent.State,
                agent.Outcome,
                agent.StartedAtUtc,
                agent.UpdatedAtUtc,
                agent.StatusBadgeText,
                agent.StatusTone)))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.AgentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ProcessLiveStats BuildLiveStats(
        IReadOnlyList<ProcessRunListItem> observedRuns,
        IReadOnlyList<ProcessActiveRunSummaryViewModel> activeRunSummaries,
        IReadOnlyList<AgentRunMetric> metrics)
    {
        return new ProcessLiveStats(
            observedRuns.Count,
            observedRuns.Count(item => item.Status == ProcessRunStatus.Active),
            observedRuns.Count(item => item.Status == ProcessRunStatus.Blocked),
            observedRuns.Count(item => item.Status == ProcessRunStatus.Failed),
            activeRunSummaries.Sum(item => item.ActiveExecutionCount),
            activeRunSummaries.Sum(item => item.PendingApprovalCount),
            activeRunSummaries.Sum(item => item.PendingOutboxCount),
            activeRunSummaries.Sum(item => item.DeadLetteredOutboxCount),
            metrics.Sum(item => item.DurationMs),
            ClampToInt(metrics.Sum(item => (long)item.InputTokens)),
            ClampToInt(metrics.Sum(item => (long)item.OutputTokens)),
            ClampToInt(metrics.Sum(item => (long)item.ToolCalls)),
            observedRuns.Sum(item => item.EstimatedCost),
            observedRuns.Sum(item => item.ActualCost));
    }

    private static IReadOnlyList<ProcessLiveMetricPoint> BuildLiveMetricPoints(
        IReadOnlyList<AgentRunMetric> metrics,
        ProcessLiveHistoryWindow historyWindow)
    {
        if (metrics.Count == 0)
        {
            return [];
        }

        var bucketSpan = ResolveMetricBucketSpan(historyWindow);
        var buckets = new Dictionary<DateTimeOffset, LiveMetricAccumulator>();
        foreach (var metric in metrics)
        {
            var bucket = FloorToBucket(metric.CreatedAtUtc, bucketSpan);
            if (!buckets.TryGetValue(bucket, out var accumulator))
            {
                accumulator = new LiveMetricAccumulator();
                buckets[bucket] = accumulator;
            }

            accumulator.InputTokens += metric.InputTokens;
            accumulator.OutputTokens += metric.OutputTokens;
            accumulator.DurationMs += metric.DurationMs;
            accumulator.ToolCalls += metric.ToolCalls;
        }

        return buckets
            .OrderBy(item => item.Key)
            .Select(item => new ProcessLiveMetricPoint(
                item.Key,
                ClampToInt(item.Value.InputTokens),
                ClampToInt(item.Value.OutputTokens),
                item.Value.DurationMs,
                ClampToInt(item.Value.ToolCalls)))
            .ToArray();
    }

    private static IReadOnlyList<ProcessLiveToolUsage> BuildLiveToolUsage(
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        if (toolReceipts.Count == 0)
        {
            return [];
        }

        return toolReceipts
            .GroupBy(item => new ProcessLiveToolUsageKey(
                NormalizeToolLabel(item.ToolFamily, "tool"),
                NormalizeToolLabel(item.ToolName, "unknown")))
            .Select(group => new ProcessLiveToolUsage(
                group.Key.ToolName,
                group.Key.ToolFamily,
                group.Count(),
                group.Max(item => item.CompletedAtUtc)))
            .OrderByDescending(item => item.CallCount)
            .ThenByDescending(item => item.LastUsedAtUtc)
            .ThenBy(item => item.ToolName, StringComparer.OrdinalIgnoreCase)
            .Take(LiveToolUsageLimit)
            .ToArray();
    }

    private static IReadOnlyList<ProcessObservationDialogDescriptor> BuildDialogDescriptors(
        IReadOnlyList<ProcessRunListItem> runs)
    {
        if (runs.Count == 0)
        {
            return [];
        }

        var descriptors = new List<ProcessObservationDialogDescriptor>(runs.Count);
        foreach (var run in runs)
        {
            descriptors.Add(new ProcessObservationDialogDescriptor(
                ProcessObservationDialogKind.RunSteps,
                ProcessObservationFocusKind.Run,
                run.Id,
                StepRunId: null,
                run.Name,
                $"{run.Status} / {run.TotalStepCount} steps"));
        }

        return descriptors;
    }

    private static IReadOnlyList<ProcessObservationTimelineItem> ToTimelineItems(
        IReadOnlyList<ProcessAttemptTimelineEntryViewModel> timeline,
        Guid? stepRunId = null)
    {
        var items = new List<ProcessObservationTimelineItem>(timeline.Count);
        foreach (var item in timeline)
        {
            if (stepRunId.HasValue && item.StepRunId != stepRunId.Value)
            {
                continue;
            }

            items.Add(new ProcessObservationTimelineItem(
                item.Kind,
                item.StepRunId,
                item.StepTitle,
                item.ExecutionRunId,
                item.OutboxRecordId,
                item.EscalationId,
                item.Title,
                item.Status,
                item.StatusTone,
                item.Summary,
                item.OccurredAtUtc));
        }

        items.Sort(static (left, right) => right.OccurredAtUtc.CompareTo(left.OccurredAtUtc));
        return items;
    }

    private static IReadOnlyList<ProcessObservationTimelineItem> PageItems(
        IReadOnlyList<ProcessObservationTimelineItem> allItems,
        int skip,
        int take)
    {
        if (skip >= allItems.Count)
        {
            return [];
        }

        var pageCount = Math.Min(take, allItems.Count - skip);
        var page = new List<ProcessObservationTimelineItem>(pageCount);
        for (var index = skip; index < skip + pageCount; index++)
        {
            page.Add(allItems[index]);
        }

        return page;
    }

    private static DateTimeOffset? ResolveSourceMaxUpdatedAtUtc(
        IReadOnlyList<ProcessRunListItem> runs,
        IReadOnlyList<ProcessActiveRunSummaryViewModel> activeRunSummaries)
    {
        var maxRunUpdatedAtUtc = FindMaxUpdatedAtUtc(runs);
        var maxActiveUpdatedAtUtc = FindMaxUpdatedAtUtc(activeRunSummaries);

        return (maxRunUpdatedAtUtc, maxActiveUpdatedAtUtc) switch
        {
            ({ } left, { } right) => left > right ? left : right,
            ({ } left, null) => left,
            (null, { } right) => right,
            _ => null
        };
    }

    private static DateTimeOffset? FindMaxUpdatedAtUtc(IReadOnlyList<ProcessRunListItem> runs)
    {
        DateTimeOffset? maxUpdatedAtUtc = null;
        foreach (var run in runs)
        {
            if (!maxUpdatedAtUtc.HasValue || run.UpdatedAtUtc > maxUpdatedAtUtc.Value)
            {
                maxUpdatedAtUtc = run.UpdatedAtUtc;
            }
        }

        return maxUpdatedAtUtc;
    }

    private static DateTimeOffset? FindMaxUpdatedAtUtc(IReadOnlyList<ProcessActiveRunSummaryViewModel> summaries)
    {
        DateTimeOffset? maxUpdatedAtUtc = null;
        foreach (var summary in summaries)
        {
            if (!maxUpdatedAtUtc.HasValue || summary.UpdatedAtUtc > maxUpdatedAtUtc.Value)
            {
                maxUpdatedAtUtc = summary.UpdatedAtUtc;
            }
        }

        return maxUpdatedAtUtc;
    }

    private static DateTimeOffset? ResolveLiveSourceMaxUpdatedAtUtc(
        IReadOnlyList<ProcessRunListItem> runs,
        IReadOnlyList<ProcessActiveRunSummaryViewModel> activeRunSummaries,
        IReadOnlyList<ProcessLiveEscalationCard> escalationCards,
        IReadOnlyList<ProcessLiveRunEventCard> runEventCards,
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        IReadOnlyList<AgentRunMetric> metrics,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        var candidates = new List<DateTimeOffset>();
        AddIfPresent(candidates, FindMaxUpdatedAtUtc(runs));
        AddIfPresent(candidates, FindMaxUpdatedAtUtc(activeRunSummaries));
        AddIfPresent(candidates, FindMax(escalationCards, item => item.UpdatedAtUtc));
        AddIfPresent(candidates, FindMax(runEventCards, item => item.OccurredAtUtc));
        AddIfPresent(candidates, FindMax(executionRuns, item => item.UpdatedAtUtc));
        AddIfPresent(candidates, FindMax(metrics, item => item.CreatedAtUtc));
        AddIfPresent(candidates, FindMax(toolReceipts, item => item.CompletedAtUtc));

        return candidates.Count == 0
            ? null
            : candidates.Max();
    }

    private static DateTimeOffset? FindMax<T>(
        IReadOnlyList<T> items,
        Func<T, DateTimeOffset> selector)
    {
        DateTimeOffset? maxValue = null;
        foreach (var item in items)
        {
            var value = selector(item);
            if (!maxValue.HasValue || value > maxValue.Value)
            {
                maxValue = value;
            }
        }

        return maxValue;
    }

    private static void AddIfPresent(
        List<DateTimeOffset> values,
        DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            values.Add(value.Value);
        }
    }

    private static bool ShouldIncludeLiveRun(
        ProcessRunListItem run,
        Guid? processRunId)
    {
        return processRunId.HasValue
            ? run.Id == processRunId.Value
            : IsLiveObservedRunStatus(run.Status);
    }

    private static bool IsLiveObservedRunStatus(ProcessRunStatus status)
    {
        return status is ProcessRunStatus.Active or ProcessRunStatus.Blocked or ProcessRunStatus.Failed;
    }

    private static bool IsLiveRunEventStatus(ProcessRunStatus status)
    {
        return status is ProcessRunStatus.Completed or ProcessRunStatus.Blocked or ProcessRunStatus.Failed;
    }

    private static int ResolveLiveRunSortRank(ProcessRunStatus status)
    {
        return status switch
        {
            ProcessRunStatus.Blocked => 0,
            ProcessRunStatus.Failed => 1,
            ProcessRunStatus.Active => 2,
            _ => 3
        };
    }

    private static int ResolveLiveRunEventSortRank(ProcessRunStatus status)
    {
        return status switch
        {
            ProcessRunStatus.Failed => 0,
            ProcessRunStatus.Blocked => 1,
            ProcessRunStatus.Completed => 2,
            _ => 3
        };
    }

    private static string BuildLiveEscalationKey(ProcessEscalationViewModel escalation)
    {
        return $"escalation:{escalation.Id:N}:{escalation.Status}:{escalation.UpdatedAtUtc.UtcTicks}";
    }

    private static string BuildLiveRunEventKey(ProcessRunListItem run)
    {
        return $"run-event:{run.Id:N}:{run.Status}:{run.UpdatedAtUtc.UtcTicks}";
    }

    private static string ResolveLiveRunEventTitle(ProcessRunListItem run)
    {
        return run.Status switch
        {
            ProcessRunStatus.Completed => "Process finished successfully",
            ProcessRunStatus.Blocked => "Process is blocked",
            ProcessRunStatus.Failed => "Process failed",
            _ => "Process status changed"
        };
    }

    private static string ResolveLiveRunEventSummary(ProcessRunListItem run)
    {
        return run.Status switch
        {
            ProcessRunStatus.Completed => $"{run.Name} completed {run.CompletedStepCount}/{run.TotalStepCount} step(s).",
            ProcessRunStatus.Blocked => $"{run.Name} is blocked with {run.BlockedStepCount} blocked step(s).",
            ProcessRunStatus.Failed => $"{run.Name} failed after {run.CompletedStepCount}/{run.TotalStepCount} completed step(s).",
            _ => $"{run.Name} changed to {run.Status}."
        };
    }

    private static string ResolveLiveRunEventIcon(ProcessRunStatus status)
    {
        return status switch
        {
            ProcessRunStatus.Completed => "check_circle",
            ProcessRunStatus.Blocked => "block",
            ProcessRunStatus.Failed => "error",
            _ => "notifications"
        };
    }

    private static string ResolveDefinitionName(
        Guid definitionId,
        IReadOnlyDictionary<Guid, ProcessDefinitionListItem> definitionsById)
    {
        return definitionsById.TryGetValue(definitionId, out var definition) &&
               !string.IsNullOrWhiteSpace(definition.Name)
            ? definition.Name
            : "Process definition";
    }

    private static string NormalizeToolLabel(
        string? value,
        string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static Guid? TryParseGuid(string value)
    {
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    private static ProcessLiveHistoryWindow NormalizeHistoryWindow(ProcessLiveHistoryWindow historyWindow)
    {
        return Enum.IsDefined(historyWindow)
            ? historyWindow
            : ProcessLiveHistoryWindow.LiveHour;
    }

    private static TimeSpan ResolveHistoryWindowSpan(ProcessLiveHistoryWindow historyWindow)
    {
        return historyWindow switch
        {
            ProcessLiveHistoryWindow.OneDay => TimeSpan.FromDays(1),
            ProcessLiveHistoryWindow.SevenDays => TimeSpan.FromDays(7),
            ProcessLiveHistoryWindow.ThirtyDays => TimeSpan.FromDays(30),
            _ => TimeSpan.FromHours(1)
        };
    }

    private static TimeSpan ResolveMetricBucketSpan(ProcessLiveHistoryWindow historyWindow)
    {
        return historyWindow switch
        {
            ProcessLiveHistoryWindow.OneDay => TimeSpan.FromHours(1),
            ProcessLiveHistoryWindow.SevenDays => TimeSpan.FromHours(6),
            ProcessLiveHistoryWindow.ThirtyDays => TimeSpan.FromDays(1),
            _ => TimeSpan.FromMinutes(5)
        };
    }

    private static int ResolveLiveExecutionRunTake(ProcessLiveHistoryWindow historyWindow)
    {
        return historyWindow switch
        {
            ProcessLiveHistoryWindow.OneDay => 1000,
            ProcessLiveHistoryWindow.SevenDays => 2500,
            ProcessLiveHistoryWindow.ThirtyDays => 5000,
            _ => 500
        };
    }

    private static int ResolveLiveExecutionRunDetailTake(ProcessLiveHistoryWindow historyWindow)
    {
        return historyWindow switch
        {
            ProcessLiveHistoryWindow.OneDay => 180,
            ProcessLiveHistoryWindow.SevenDays => 240,
            ProcessLiveHistoryWindow.ThirtyDays => 300,
            _ => 120
        };
    }

    private static DateTimeOffset FloorToBucket(
        DateTimeOffset timestamp,
        TimeSpan bucketSpan)
    {
        var utcTicks = timestamp.UtcTicks;
        var bucketTicks = bucketSpan.Ticks;
        if (bucketTicks <= 0)
        {
            return timestamp.ToUniversalTime();
        }

        return new DateTimeOffset(utcTicks - utcTicks % bucketTicks, TimeSpan.Zero);
    }

    private static int ClampToInt(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        return value >= int.MaxValue
            ? int.MaxValue
            : (int)value;
    }

    private static string BuildDashboardFingerprint(ProcessObservationDashboardQuery query)
    {
        return string.Join(
            "|",
            query.IncludeRuns ? "runs" : "no-runs",
            query.IncludeActiveRunSummaries ? "active" : "no-active",
            query.IncludeAnalytics ? "analytics" : "no-analytics");
    }

    private static ProcessObservationStaleness BuildStaleness<T>(ProcessObservationCacheResult<T> result)
    {
        return new ProcessObservationStaleness(
            result.Status == ProcessObservationCacheStatus.Hit
                ? ProcessObservationFreshness.Cached
                : ProcessObservationFreshness.Fresh,
            result.StoredAtUtc,
            result.ExpiresAtUtc);
    }

    private sealed record LiveExecutionRunMatch(
        Guid? ProcessRunId,
        ExecutionRunRecord ExecutionRun);

    private sealed record ProcessLiveToolUsageKey(
        string ToolFamily,
        string ToolName);

    private sealed class LiveMetricAccumulator
    {
        public long InputTokens { get; set; }

        public long OutputTokens { get; set; }

        public long DurationMs { get; set; }

        public long ToolCalls { get; set; }
    }
}
