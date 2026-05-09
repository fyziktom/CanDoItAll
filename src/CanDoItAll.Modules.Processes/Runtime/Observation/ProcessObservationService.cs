using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessObservationService(
    ProcessesService processesService,
    ProcessWorkspaceRunDetailsLoader runDetailsLoader,
    ProcessRuntimeStateOverviewService runtimeStateOverviewService,
    ProcessObservationCache cache,
    IOptions<ProcessObservationCacheOptions> options,
    ILogger<ProcessObservationService> logger) : IProcessObservationService
{
    private const int MaxTimelineTake = 200;

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
}
