using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record WorkspaceChatProjection(
    IReadOnlyList<ChatSessionSummaryRecord> SessionSummaries,
    IReadOnlyList<ChatRunSummaryRecord> RunSummaries,
    IReadOnlyDictionary<Guid, ExecutionRunRecord> LatestRunBySessionId);

internal static class WorkspaceChatProjectionBuilder
{
    public const int CurrentReportingProjectionVersion = 3;

    private const int MaximumReportingTitleLength = 120;
    private const int MaximumReportingSummaryLength = 280;
    private const string ProjectStructureSourceKind = "project-structure";
    private const string ProjectsSourceKind = "projects";
    private const string ProjectsWorkspaceSourceId = "projects";
    private const string ProcessesSourceKind = "processes";
    private const string LiveProcessesSourceKind = "processes-live";
    private const string ProcessProjectSourceSegment = ":project:";
    private const string ProcessGlobalSourceSuffix = ":global";

    public static WorkspaceChatProjection Build(
        IReadOnlyList<ChatSessionRecord> sessions,
        IReadOnlyList<ExecutionRunRecord> runs,
        IReadOnlyList<ExecutionLogEntry> executionLog,
        IReadOnlyList<AgentRunMetric>? metrics = null,
        IReadOnlyList<ProviderUsageObservation>? usageObservations = null)
    {
        var logsByRun = executionLog
            .GroupBy(item => item.ExecutionRunId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ExecutionLogEntry>)group.ToList());
        var metricsByRun = metrics?
            .GroupBy(item => item.ExecutionRunId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<AgentRunMetric>)group.ToList());
        var usageByRun = usageObservations?
            .Where(item => item.ExecutionRunId.HasValue)
            .GroupBy(item => item.ExecutionRunId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProviderUsageObservation>)group.ToList());
        var latestRunBySessionId = BuildLatestRunBySessionId(runs);
        var runSummaries = new List<ChatRunSummaryRecord>(runs.Count);
        foreach (var run in runs)
        {
            var runLogs = logsByRun.TryGetValue(run.Id, out var indexedLogs)
                ? indexedLogs
                : [];
            if (metricsByRun is not null && usageByRun is not null)
            {
                runSummaries.Add(CreateChatRunSummary(
                    run,
                    runLogs,
                    metricsByRun.TryGetValue(run.Id, out var indexedMetrics)
                        ? indexedMetrics
                        : [],
                    usageByRun.TryGetValue(run.Id, out var indexedUsage)
                        ? indexedUsage
                        : []));
                continue;
            }

            runSummaries.Add(CreateChatRunSummary(run, runLogs));
        }

        return new WorkspaceChatProjection(
            SessionSummaries: sessions
                .Select(session => CreateChatSessionSummary(
                    session,
                    latestRunBySessionId.TryGetValue(session.Id, out var latestRun) ? latestRun : null))
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList(),
            RunSummaries: runSummaries
                .OrderByDescending(static item => item.ActivityAtUtc)
                .ThenByDescending(static item => item.CreatedAtUtc)
                .ThenBy(static item => item.ExecutionRunId)
                .ToList(),
            LatestRunBySessionId: latestRunBySessionId);
    }

    public static ChatSessionSummaryRecord CreateChatSessionSummary(
        ChatSessionRecord session,
        ExecutionRunRecord? latestRun = null)
    {
        var lastMessage = session.Messages.LastOrDefault();
        var preview = lastMessage?.Content ?? "No messages yet.";
        if (preview.Length > 180)
        {
            preview = $"{preview[..177].TrimEnd()}...";
        }

        return new ChatSessionSummaryRecord(
            session.Id,
            session.AgentId,
            session.Title,
            session.CreatedAtUtc,
            session.UpdatedAtUtc,
            session.Messages.Count,
            preview,
            latestRun?.PendingApprovals.Count ?? session.Compatibility?.PendingApprovals.Count ?? 0,
            latestRun?.AutoApprovePendingToolCalls ?? session.Compatibility?.AutoApprovePendingToolCalls ?? false);
    }

    public static ChatRunSummaryRecord CreateChatRunSummary(
        ExecutionRunRecord run,
        IEnumerable<ExecutionLogEntry> executionLog)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(executionLog);

        var latestEntry = ResolveLatestExecutionLogEntry(executionLog);
        var projectAttribution = ResolveProjectAttribution(run);
        var processRunId = run.ProcessRunId?.Trim() ?? string.Empty;
        var correlations = ResolveTypedCorrelations(processRunId, []);
        return CreateLegacyChatRunSummary(run, latestEntry) with
        {
            Title = CreatePreview(run.Title, MaximumReportingTitleLength),
            Summary = ResolveSummary(run, latestEntry),
            SourceKind = run.SourceKind?.Trim() ?? string.Empty,
            SourceId = run.SourceId?.Trim() ?? string.Empty,
            ProjectId = projectAttribution.ProjectId,
            ProjectAttributionSource = projectAttribution.Source,
            ProcessRunId = processRunId,
            CorrelatedProcessRunId = correlations.ProcessRunId,
            InvalidCorrelationIdCount = correlations.InvalidIdCount,
            CreatedAtUtc = run.CreatedAtUtc,
            StartedAtUtc = run.StartedAtUtc,
            CompletedAtUtc = run.CompletedAtUtc,
            Duration = ResolveWallClockDuration(run),
            HasUnknownCost = true
        };
    }

    public static ChatRunSummaryRecord CreateLegacyChatRunSummary(
        ExecutionRunRecord run,
        IEnumerable<ExecutionLogEntry> executionLog)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(executionLog);
        return CreateLegacyChatRunSummary(
            run,
            ResolveLatestExecutionLogEntry(executionLog));
    }

    public static ChatRunSummaryRecord CreateChatRunSummary(ExecutionRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return CreateChatRunSummary(
            detail.Run,
            detail.ExecutionLog,
            detail.Metrics,
            detail.UsageObservations);
    }

    public static IReadOnlyDictionary<Guid, ExecutionRunRecord> BuildLatestRunBySessionId(
        IReadOnlyList<ExecutionRunRecord> runs)
    {
        return runs
            .Where(run => run.ChatSessionId.HasValue)
            .GroupBy(run => run.ChatSessionId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(run => run.UpdatedAtUtc)
                    .ThenByDescending(run => run.CreatedAtUtc)
                    .First());
    }

    private static ChatRunSummaryRecord CreateChatRunSummary(
        ExecutionRunRecord run,
        IReadOnlyList<ExecutionLogEntry> executionLog,
        IReadOnlyList<AgentRunMetric> metrics,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        var summary = CreateChatRunSummary(run, executionLog);
        var cost = ResolveCost(metrics, usageObservations);
        var processRunId = ResolveProcessRunId(run, usageObservations);
        var workflowRunIds = ResolveWorkflowRunIds(usageObservations);
        var correlations = ResolveTypedCorrelations(
            processRunId,
            workflowRunIds);
        return summary with
        {
            ProcessRunId = processRunId,
            CorrelatedProcessRunId = correlations.ProcessRunId,
            WorkflowRunIds = workflowRunIds,
            CorrelatedWorkflowRunIds = correlations.WorkflowRunIds,
            InvalidCorrelationIdCount = correlations.InvalidIdCount,
            KnownCostUsd = cost.KnownCostUsd,
            HasUnknownCost = cost.HasUnknownCost,
            ReportingProjectionVersion = CurrentReportingProjectionVersion
        };
    }

    private static ChatRunSummaryRecord CreateLegacyChatRunSummary(
        ExecutionRunRecord run,
        ExecutionLogEntry? latestEntry)
    {
        return new ChatRunSummaryRecord(
            run.Id,
            run.AgentId,
            run.ChatSessionId,
            run.UpdatedAtUtc,
            latestEntry?.State ?? run.State,
            latestEntry?.Phase ?? "Run",
            latestEntry?.Message ??
            (!string.IsNullOrWhiteSpace(run.ResultSummary)
                ? run.ResultSummary
                : run.Title),
            run.Outcome);
    }

    private static ExecutionLogEntry? ResolveLatestExecutionLogEntry(
        IEnumerable<ExecutionLogEntry> executionLog)
    {
        ExecutionLogEntry? latestEntry = null;
        foreach (var entry in executionLog)
        {
            if (latestEntry is null ||
                entry.CreatedAtUtc > latestEntry.CreatedAtUtc)
            {
                latestEntry = entry;
            }
        }

        return latestEntry;
    }

    private static string ResolveSummary(
        ExecutionRunRecord run,
        ExecutionLogEntry? latestEntry)
    {
        var value = !string.IsNullOrWhiteSpace(run.ResultSummary)
            ? run.ResultSummary
            : !string.IsNullOrWhiteSpace(run.InputSummary)
                ? run.InputSummary
                : latestEntry?.Message ?? run.Title;
        return CreatePreview(value, MaximumReportingSummaryLength);
    }

    private static string CreatePreview(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var preview = value.Trim().ReplaceLineEndings(" ");
        if (preview.Length <= maximumLength)
        {
            return preview;
        }

        return $"{preview[..(maximumLength - 3)].TrimEnd()}...";
    }

    private static TimeSpan? ResolveWallClockDuration(ExecutionRunRecord run)
    {
        if (!run.StartedAtUtc.HasValue)
        {
            return null;
        }

        var completedAtUtc = run.CompletedAtUtc ?? run.UpdatedAtUtc;
        return completedAtUtc >= run.StartedAtUtc.Value
            ? completedAtUtc - run.StartedAtUtc.Value
            : null;
    }

    private static AgentExecutionProjectAttribution ResolveProjectAttribution(
        ExecutionRunRecord run)
    {
        var recordedScope =
            ExecutionInvocationMetadata.ResolveRecordedContextWorkspaceScopeForReporting(run);
        if (recordedScope.IsPresent)
        {
            if (!recordedScope.IsValid)
            {
                return new AgentExecutionProjectAttribution(
                    ProjectId: null,
                    AgentExecutionProjectAttributionSource.InvalidRecordedScope);
            }

            if (recordedScope.Scope?.Kind != WorkspaceScopeKind.Project)
            {
                return AgentExecutionProjectAttribution.None;
            }

            return TryParseProjectId(recordedScope.Scope.Key, out var recordedProjectId)
                ? new AgentExecutionProjectAttribution(
                    recordedProjectId,
                    AgentExecutionProjectAttributionSource.RecordedScope)
                : new AgentExecutionProjectAttribution(
                    ProjectId: null,
                    AgentExecutionProjectAttributionSource.InvalidRecordedScope);
        }

        return ResolveLegacyProjectAttribution(run.SourceKind, run.SourceId);
    }

    private static AgentExecutionProjectAttribution ResolveLegacyProjectAttribution(
        string? sourceKind,
        string? sourceId)
    {
        var normalizedSourceKind = sourceKind?.Trim() ?? string.Empty;
        var normalizedSourceId = sourceId?.Trim() ?? string.Empty;
        if (string.Equals(
                normalizedSourceKind,
                ProjectStructureSourceKind,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                normalizedSourceKind,
                ProjectsSourceKind,
                StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(
                    normalizedSourceKind,
                    ProjectsSourceKind,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    normalizedSourceId,
                    ProjectsWorkspaceSourceId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return AgentExecutionProjectAttribution.None;
            }

            return TryParseProjectId(normalizedSourceId, out var directProjectId)
                ? new AgentExecutionProjectAttribution(
                    directProjectId,
                    AgentExecutionProjectAttributionSource.LegacySource)
                : new AgentExecutionProjectAttribution(
                    ProjectId: null,
                    AgentExecutionProjectAttributionSource.InvalidLegacySource);
        }

        if (!string.Equals(
                normalizedSourceKind,
                ProcessesSourceKind,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                normalizedSourceKind,
                LiveProcessesSourceKind,
                StringComparison.OrdinalIgnoreCase))
        {
            return AgentExecutionProjectAttribution.None;
        }

        if (normalizedSourceId.EndsWith(
                ProcessGlobalSourceSuffix,
                StringComparison.OrdinalIgnoreCase))
        {
            return AgentExecutionProjectAttribution.None;
        }

        var projectSegmentIndex = normalizedSourceId.IndexOf(
            ProcessProjectSourceSegment,
            StringComparison.OrdinalIgnoreCase);
        if (projectSegmentIndex < 0)
        {
            return new AgentExecutionProjectAttribution(
                ProjectId: null,
                AgentExecutionProjectAttributionSource.InvalidLegacySource);
        }

        var projectIdValue = normalizedSourceId[
            (projectSegmentIndex + ProcessProjectSourceSegment.Length)..];
        return TryParseProjectId(projectIdValue, out var processProjectId)
            ? new AgentExecutionProjectAttribution(
                processProjectId,
                AgentExecutionProjectAttributionSource.LegacySource)
            : new AgentExecutionProjectAttribution(
                ProjectId: null,
                AgentExecutionProjectAttributionSource.InvalidLegacySource);
    }

    private static bool TryParseProjectId(string? value, out Guid projectId)
    {
        return Guid.TryParse(value, out projectId) &&
            projectId != Guid.Empty;
    }

    private static (decimal KnownCostUsd, bool HasUnknownCost) ResolveCost(
        IReadOnlyList<AgentRunMetric> metrics,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        var hasUnknownObservationCost = false;
        if (usageObservations.Count > 0)
        {
            var knownCostUsd = 0m;
            var hasKnownCost = false;
            foreach (var observation in usageObservations)
            {
                if (ProviderPricingCalculator.TryResolveObservationCost(
                        observation,
                        providers: [],
                        out var observationCostUsd))
                {
                    hasKnownCost = true;
                    knownCostUsd += observationCostUsd;
                }
                else
                {
                    hasUnknownObservationCost = true;
                }
            }

            if (hasKnownCost)
            {
                return (knownCostUsd, hasUnknownObservationCost);
            }
        }

        var metricCostUsd = 0m;
        var hasKnownMetricCost = false;
        foreach (var metric in metrics)
        {
            if (metric.CostUsd <= 0m)
            {
                continue;
            }

            hasKnownMetricCost = true;
            metricCostUsd += metric.CostUsd;
        }

        return (
            metricCostUsd,
            hasUnknownObservationCost || !hasKnownMetricCost);
    }

    private static string ResolveProcessRunId(
        ExecutionRunRecord run,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        if (!string.IsNullOrWhiteSpace(run.ProcessRunId))
        {
            return run.ProcessRunId.Trim();
        }

        foreach (var observation in usageObservations)
        {
            if (!string.IsNullOrWhiteSpace(observation.ProcessRunId))
            {
                return observation.ProcessRunId.Trim();
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveWorkflowRunIds(
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        return usageObservations
            .Select(static observation => observation.WorkflowRunId?.Trim())
            .Where(static workflowRunId => !string.IsNullOrWhiteSpace(workflowRunId))
            .Select(static workflowRunId => workflowRunId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                static workflowRunId => workflowRunId,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static AgentExecutionCorrelations ResolveTypedCorrelations(
        string processRunId,
        IReadOnlyList<string> workflowRunIds)
    {
        Guid? correlatedProcessRunId = null;
        var invalidIdCount = 0;
        if (!string.IsNullOrWhiteSpace(processRunId))
        {
            if (Guid.TryParse(processRunId, out var parsedProcessRunId) &&
                parsedProcessRunId != Guid.Empty)
            {
                correlatedProcessRunId = parsedProcessRunId;
            }
            else
            {
                invalidIdCount++;
            }
        }

        var correlatedWorkflowRunIds = new HashSet<Guid>();
        foreach (var workflowRunId in workflowRunIds)
        {
            if (Guid.TryParse(workflowRunId, out var parsedWorkflowRunId) &&
                parsedWorkflowRunId != Guid.Empty)
            {
                correlatedWorkflowRunIds.Add(parsedWorkflowRunId);
            }
            else
            {
                invalidIdCount++;
            }
        }

        return new AgentExecutionCorrelations(
            correlatedProcessRunId,
            correlatedWorkflowRunIds
                .Order()
                .ToArray(),
            invalidIdCount);
    }

    private readonly record struct AgentExecutionProjectAttribution(
        Guid? ProjectId,
        AgentExecutionProjectAttributionSource Source)
    {
        public static AgentExecutionProjectAttribution None { get; } =
            new(
                ProjectId: null,
                AgentExecutionProjectAttributionSource.None);
    }

    private readonly record struct AgentExecutionCorrelations(
        Guid? ProcessRunId,
        IReadOnlyList<Guid> WorkflowRunIds,
        int InvalidIdCount);
}
