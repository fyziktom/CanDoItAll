using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectManagerSummaryQueryService(
    ProjectPlanAnalyticsQueryService planAnalytics,
    IAgentExecutionReportReader agentWorkspace,
    ILlmChatProjectStructureReportStore simpleChatProjectStructureReportStore,
    IWorkflowProjectStructureReportStore workflowProjectStructureReportStore,
    IProcessRunRecordStore processRunRecordStore,
    ProcessDefinitionCatalogProjectionService processDefinitionCatalog,
    IClock clock)
{
    internal const int MaximumChartDayCount =
        ProjectManagerSummarySnapshotCalculator.MaximumChartDayCount;
    private const int LatestActivityCount =
        ProjectManagerSummarySnapshotCalculator.LatestActivityCount;
    private readonly ConcurrentDictionary<Guid, IReadOnlyDictionary<Guid, string>>
        processDefinitionTitlesByProject = [];

    public async Task<ProjectManagerSummarySnapshot> LoadAsync(
        ProjectManagerSummaryScopeResolution scope,
        ProjectManagerSummaryOptions options,
        Func<ProjectManagerSummaryLoadProgress, ValueTask>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(options);
        ValidateScope(scope);
        ValidateOptions(options);
        if (scope.Scope != options.Scope)
        {
            throw new ArgumentException(
                "The resolved project scope does not match the requested manager summary options.",
                nameof(scope));
        }

        var asOfUtc = clock.GetUtcNow();
        var historyFromUtc = ResolveHistoryFromUtc(options.TimeRange, asOfUtc);
        var chartFromUtc = historyFromUtc ?? asOfUtc.AddDays(-MaximumChartDayCount);
        var uncategorizedOnly = scope.Scope == ProjectManagerSummaryScope.UncategorizedAgentActivity;

        await ReportProgressAsync(
            reportProgress,
            "Reading the project plan",
            uncategorizedOnly
                ? "The uncategorized view has no project task plan."
                : $"Reading task schedules and remaining expected costs for {scope.ProjectIds.Count:N0} project(s).",
            0,
            4);
        IReadOnlyList<ProjectPlanManagerSummary> plans = uncategorizedOnly
            ? []
            : await planAnalytics.GetManagerSummariesAsync(
                scope.ProjectIds,
                new ProjectPlanManagerSummaryQuery(
                    options.ContentMode == ProjectManagerSummaryContentMode.HistoryOnly
                        ? ProjectPlanManagerSummaryMode.ScheduleOnly
                        : ProjectPlanManagerSummaryMode.ScheduleAndRemainingCosts,
                    asOfUtc),
                cancellationToken);

        await ReportProgressAsync(
            reportProgress,
            "Reading historical activity",
            uncategorizedOnly
                ? "Reading the lightweight uncategorized conversation index without loading run payloads."
                : "Reading lightweight conversations, Simple Chats, standalone workflows, and root process projections in parallel.",
            1,
            4);
        var agentReportTask = agentWorkspace.QueryExecutionReportAsync(
            BuildAgentQuery(
                scope,
                historyFromUtc,
                asOfUtc,
                ProjectManagerActivityStatusFilter.All,
                pageIndex: 0,
                pageSize: LatestActivityCount),
            cancellationToken);
        var workflowReportTask = uncategorizedOnly
            ? Task.FromResult(ProjectManagerWorkflowSummaryInput.Empty)
            : ReadWorkflowReportAsync(
                scope.ProjectIds,
                historyFromUtc,
                asOfUtc,
                chartFromUtc,
                ProjectManagerActivityStatusFilter.All,
                requestedPageIndex: 0,
                requestedPageSize: LatestActivityCount,
                includeAggregate: true,
                cancellationToken);
        var simpleChatReportTask = uncategorizedOnly
            ? Task.FromResult(ProjectManagerSimpleChatSummaryInput.Empty)
            : ReadSimpleChatReportAsync(
                scope.ProjectIds,
                historyFromUtc,
                asOfUtc,
                chartFromUtc,
                ProjectManagerActivityStatusFilter.All,
                requestedPageIndex: 0,
                requestedPageSize: LatestActivityCount,
                includeAggregate: true,
                cancellationToken);
        var processSummaryTask = uncategorizedOnly
            ? Task.FromResult(ProcessSummary.Empty)
            : ReadProcessSummaryAsync(
                scope.ProjectIds,
                historyFromUtc,
                asOfUtc,
                chartFromUtc,
                ProjectManagerActivityStatusFilter.All,
                cancellationToken);
        await Task.WhenAll(
            agentReportTask,
            simpleChatReportTask,
            workflowReportTask,
            processSummaryTask);
        var agentReport = await agentReportTask;
        var simpleChatReport = await simpleChatReportTask;
        var workflowReport = await workflowReportTask;
        var processSummary = await processSummaryTask;

        var snapshot = ProjectManagerSummarySnapshotCalculator.Calculate(
            new ProjectManagerSummaryCompositionInput(
                scope,
                options,
                historyFromUtc,
                asOfUtc,
                clock.GetUtcNow(),
                plans,
                MapAgentSummaryInput(agentReport),
                simpleChatReport,
                workflowReport,
                processSummary.Report with
                {
                    Activities = processSummary.Page.Items
                }));
        await ReportProgressAsync(
            reportProgress,
            "Manager summary ready",
            "The retained reporting snapshot is ready.",
            4,
            4);
        return snapshot;
    }

    public async Task<ProjectManagerActivityPage> QueryActivityPageAsync(
        ProjectManagerActivityPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Summary);
        ValidateActivityPageRequest(request);

        return request.Kind switch
        {
            ProjectManagerActivityKind.Conversation => await QueryConversationPageAsync(request, cancellationToken),
            ProjectManagerActivityKind.SimpleChat => await QuerySimpleChatPageAsync(request, cancellationToken),
            ProjectManagerActivityKind.Workflow => await QueryWorkflowPageAsync(request, cancellationToken),
            ProjectManagerActivityKind.Process => await QueryProcessPageAsync(request, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Kind,
                "The manager activity kind is not supported.")
        };
    }

    internal static DateTimeOffset? ResolveHistoryFromUtc(
        ProjectManagerSummaryTimeRange range,
        DateTimeOffset asOfUtc)
        => range switch
        {
            ProjectManagerSummaryTimeRange.Day => asOfUtc.AddDays(-1),
            ProjectManagerSummaryTimeRange.Week => asOfUtc.AddDays(-7),
            ProjectManagerSummaryTimeRange.Month => asOfUtc.AddMonths(-1),
            ProjectManagerSummaryTimeRange.Quarter => asOfUtc.AddMonths(-3),
            ProjectManagerSummaryTimeRange.Year => asOfUtc.AddYears(-1),
            ProjectManagerSummaryTimeRange.All => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(range),
                range,
                "The manager summary time range is not supported.")
        };

    private async Task<ProjectManagerActivityPage> QueryConversationPageAsync(
        ProjectManagerActivityPageRequest request,
        CancellationToken cancellationToken)
    {
        var summary = request.Summary;
        var knownAggregate = request.KnownAggregate;
        var query = BuildAgentQuery(
            summary.Scope,
            summary.HistoryFromUtc,
            summary.AsOfUtc,
            request.StatusFilter,
            request.PageIndex,
            request.PageSize) with
        {
            IncludeAggregate = knownAggregate is null,
            KnownTotalCount = knownAggregate?.TotalCount
        };
        var report = await agentWorkspace.QueryExecutionReportAsync(
            query,
            cancellationToken);
        var aggregate = knownAggregate ?? new ProjectManagerActivityAggregate(
            report.TotalCount,
            new ProjectManagerCostTotals(
                report.Totals.KnownCostUsd,
                HistoricalEstimatedUsd: 0m,
                FuturePlannedUsd: 0m,
                report.Totals.UnknownCostRunCount),
            ToDurationMilliseconds(report.Totals.TotalDuration));
        return new ProjectManagerActivityPage(
            report.Items.Select(MapAgentActivity).ToArray(),
            report.PageIndex,
            report.PageSize,
            aggregate.TotalCount,
            aggregate.Totals,
            aggregate.TotalDurationMilliseconds);
    }

    private async Task<ProjectManagerActivityPage> QuerySimpleChatPageAsync(
        ProjectManagerActivityPageRequest request,
        CancellationToken cancellationToken)
    {
        var summary = request.Summary;
        if (summary.Scope.Scope == ProjectManagerSummaryScope.UncategorizedAgentActivity)
        {
            return EmptyActivityPage(request);
        }

        var chartFromUtc = summary.HistoryFromUtc ??
            summary.AsOfUtc.AddDays(-MaximumChartDayCount);
        var knownAggregate = request.KnownAggregate;
        var report = await ReadSimpleChatReportAsync(
            summary.Scope.ProjectIds,
            summary.HistoryFromUtc,
            summary.AsOfUtc,
            chartFromUtc,
            request.StatusFilter,
            request.PageIndex,
            request.PageSize,
            includeAggregate: knownAggregate is null,
            cancellationToken);
        var aggregate = knownAggregate ?? new ProjectManagerActivityAggregate(
            report.TotalCount,
            new ProjectManagerCostTotals(
                report.KnownCostUsd,
                HistoricalEstimatedUsd: 0m,
                FuturePlannedUsd: 0m,
                report.UnknownCostRunCount),
            report.DurationMilliseconds);
        return new ProjectManagerActivityPage(
            report.Activities,
            request.PageIndex,
            request.PageSize,
            aggregate.TotalCount,
            aggregate.Totals,
            aggregate.TotalDurationMilliseconds);
    }

    private async Task<ProjectManagerActivityPage> QueryWorkflowPageAsync(
        ProjectManagerActivityPageRequest request,
        CancellationToken cancellationToken)
    {
        var summary = request.Summary;
        if (summary.Scope.Scope == ProjectManagerSummaryScope.UncategorizedAgentActivity)
        {
            return EmptyActivityPage(request);
        }

        var chartFromUtc = summary.HistoryFromUtc ??
            summary.AsOfUtc.AddDays(-MaximumChartDayCount);
        var knownAggregate = request.KnownAggregate;
        var report = await ReadWorkflowReportAsync(
            summary.Scope.ProjectIds,
            summary.HistoryFromUtc,
            summary.AsOfUtc,
            chartFromUtc,
            request.StatusFilter,
            request.PageIndex,
            request.PageSize,
            includeAggregate: knownAggregate is null,
            cancellationToken);
        var aggregate = knownAggregate ?? new ProjectManagerActivityAggregate(
            report.TotalCount,
            new ProjectManagerCostTotals(
                report.KnownCostUsd,
                HistoricalEstimatedUsd: 0m,
                FuturePlannedUsd: 0m,
                report.UnknownCostRunCount),
            report.DurationMilliseconds);
        return new ProjectManagerActivityPage(
            report.Activities,
            request.PageIndex,
            request.PageSize,
            aggregate.TotalCount,
            aggregate.Totals,
            aggregate.TotalDurationMilliseconds);
    }

    private async Task<ProjectManagerActivityPage> QueryProcessPageAsync(
        ProjectManagerActivityPageRequest request,
        CancellationToken cancellationToken)
    {
        var summary = request.Summary;
        if (summary.Scope.Scope == ProjectManagerSummaryScope.UncategorizedAgentActivity ||
            request.StatusFilter == ProjectManagerActivityStatusFilter.Active)
        {
            return EmptyActivityPage(request);
        }

        var knownAggregate = request.KnownAggregate;
        var aggregate = knownAggregate;
        if (aggregate is null)
        {
            var analytics = await ReadProcessReportAsync(
                summary.Scope.ProjectIds,
                summary.HistoryFromUtc,
                summary.AsOfUtc,
                summary.HistoryFromUtc ?? summary.AsOfUtc.AddDays(-MaximumChartDayCount),
                request.StatusFilter,
                includeTrend: false,
                cancellationToken);
            aggregate = new ProjectManagerActivityAggregate(
                analytics.RunCount,
                new ProjectManagerCostTotals(
                    analytics.ActualCostUsd,
                    analytics.EstimatedCostUsd,
                    FuturePlannedUsd: 0m,
                    analytics.UnknownCostRunCount),
                analytics.DurationMilliseconds);
        }

        var page = await ReadProcessPageAsync(
            summary.Scope.ProjectIds,
            summary.HistoryFromUtc,
            summary.AsOfUtc,
            request.StatusFilter,
            request.ProcessCursor,
            request.PageSize,
            cancellationToken);
        return new ProjectManagerActivityPage(
            page.Items,
            request.PageIndex,
            request.PageSize,
            aggregate.TotalCount,
            aggregate.Totals,
            aggregate.TotalDurationMilliseconds)
        {
            NextProcessCursor = page.NextCursor
        };
    }

    private async Task<ProjectManagerSimpleChatSummaryInput> ReadSimpleChatReportAsync(
        IReadOnlyList<Guid> projectIds,
        DateTimeOffset? historyFromUtc,
        DateTimeOffset asOfUtc,
        DateTimeOffset chartFromUtc,
        ProjectManagerActivityStatusFilter statusFilter,
        int requestedPageIndex,
        int requestedPageSize,
        bool includeAggregate,
        CancellationToken cancellationToken)
    {
        var report = await simpleChatProjectStructureReportStore.QueryProjectStructureReportAsync(
            new LlmChatProjectStructureReportQuery(
                projectIds,
                historyFromUtc,
                asOfUtc,
                chartFromUtc,
                ResolveSimpleChatStatuses(statusFilter),
                requestedPageIndex,
                requestedPageSize,
                includeAggregate),
            cancellationToken);

        return new ProjectManagerSimpleChatSummaryInput(
            report.TotalCount,
            report.KnownCostUsd,
            report.TotalDurationMilliseconds,
            report.UnknownCostRunCount,
            report.DailyCost
                .Select(static item => new ProjectManagerKnownExpensePoint(
                    item.Date,
                    item.KnownCostUsd))
                .ToArray(),
            report.Runs.Select(MapSimpleChatActivity).ToArray());
    }

    private async Task<ProjectManagerWorkflowSummaryInput> ReadWorkflowReportAsync(
        IReadOnlyList<Guid> projectIds,
        DateTimeOffset? historyFromUtc,
        DateTimeOffset asOfUtc,
        DateTimeOffset chartFromUtc,
        ProjectManagerActivityStatusFilter statusFilter,
        int requestedPageIndex,
        int requestedPageSize,
        bool includeAggregate,
        CancellationToken cancellationToken)
    {
        var report = await workflowProjectStructureReportStore.QueryProjectStructureReportAsync(
            new WorkflowProjectStructureReportQuery(
                projectIds,
                historyFromUtc,
                asOfUtc,
                chartFromUtc,
                ResolveWorkflowStates(statusFilter),
                requestedPageIndex,
                requestedPageSize,
                includeAggregate),
            cancellationToken);

        return new ProjectManagerWorkflowSummaryInput(
            report.TotalCount,
            report.KnownCostUsd,
            report.TotalDurationMilliseconds,
            report.UnknownCostRunCount,
            report.DailyCost
                .Select(static item => new ProjectManagerKnownExpensePoint(
                    item.Date,
                    item.KnownCostUsd))
                .ToArray(),
            report.Runs.Select(MapWorkflowActivity).ToArray());
    }

    private async Task<ProjectManagerProcessSummaryInput> ReadProcessReportAsync(
        IReadOnlyList<Guid> projectIds,
        DateTimeOffset? historyFromUtc,
        DateTimeOffset asOfUtc,
        DateTimeOffset chartFromUtc,
        ProjectManagerActivityStatusFilter statusFilter,
        bool includeTrend,
        CancellationToken cancellationToken)
    {
        if (statusFilter == ProjectManagerActivityStatusFilter.Active)
        {
            return ProjectManagerProcessSummaryInput.Empty;
        }

        var disposition = ResolveProcessDisposition(statusFilter);
        var result = new ProcessReportAccumulator();
        foreach (var projectIdChunk in ChunkProjectIds(projectIds))
        {
            var allTime = historyFromUtc is null;
            var totals = await processRunRecordStore.ReadAnalyticsAsync(
                new ProcessRunRecordAnalyticsQuery(
                    historyFromUtc ?? chartFromUtc,
                    asOfUtc)
                {
                    ProjectIds = projectIdChunk,
                    Disposition = disposition,
                    RootRunsOnly = true,
                    AllTime = allTime,
                    IncludeDailyCostTrend = includeTrend && !allTime
                },
                cancellationToken);
            result.AddTotals(totals);

            if (!includeTrend)
            {
                continue;
            }

            if (!allTime)
            {
                result.AddTrend(totals.DailyCostTrend);
                continue;
            }

            var boundedTrend = await processRunRecordStore.ReadAnalyticsAsync(
                new ProcessRunRecordAnalyticsQuery(chartFromUtc, asOfUtc)
                {
                    ProjectIds = projectIdChunk,
                    Disposition = disposition,
                    RootRunsOnly = true,
                    IncludeTotals = false,
                    IncludeDailyCostTrend = true
                },
                cancellationToken);
            result.AddTrend(boundedTrend.DailyCostTrend);
        }

        return result.Build();
    }

    private async Task<ProcessSummary> ReadProcessSummaryAsync(
        IReadOnlyList<Guid> projectIds,
        DateTimeOffset? historyFromUtc,
        DateTimeOffset asOfUtc,
        DateTimeOffset chartFromUtc,
        ProjectManagerActivityStatusFilter statusFilter,
        CancellationToken cancellationToken)
    {
        var report = await ReadProcessReportAsync(
            projectIds,
            historyFromUtc,
            asOfUtc,
            chartFromUtc,
            statusFilter,
            includeTrend: true,
            cancellationToken);
        var page = await ReadProcessPageAsync(
            projectIds,
            historyFromUtc,
            asOfUtc,
            statusFilter,
            cursor: null,
            pageSize: LatestActivityCount,
            cancellationToken);
        return new ProcessSummary(report, page);
    }

    private async Task<ProcessPage> ReadProcessPageAsync(
        IReadOnlyList<Guid> projectIds,
        DateTimeOffset? historyFromUtc,
        DateTimeOffset asOfUtc,
        ProjectManagerActivityStatusFilter statusFilter,
        ProcessRunRecordCursor? cursor,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (statusFilter == ProjectManagerActivityStatusFilter.Active)
        {
            return ProcessPage.Empty;
        }

        var disposition = ResolveProcessDisposition(statusFilter);
        var records = new List<ProcessRunRecordSummary>();
        var anyChunkHasMore = false;
        foreach (var projectIdChunk in ChunkProjectIds(projectIds))
        {
            var page = await processRunRecordStore.ListAsync(
                new ProcessRunRecordListQuery(pageSize)
                {
                    ProjectIds = projectIdChunk,
                    Disposition = disposition,
                    RootRunsOnly = true,
                    EndedFromUtc = historyFromUtc,
                    EndedBeforeUtc = asOfUtc,
                    Cursor = cursor
                },
                cancellationToken);
            records.AddRange(page.Records);
            anyChunkHasMore |= page.NextCursor is not null;
        }

        var ordered = records
            .OrderByDescending(static record => record.Metrics.EndedAtUtc)
            .ThenByDescending(static record => record.Identity.RunId.Value)
            .ToArray();
        var selected = ordered.Take(pageSize).ToArray();
        var hasMore = anyChunkHasMore || ordered.Length > selected.Length;
        var nextCursor = hasMore && selected.Length > 0
            ? new ProcessRunRecordCursor(
                selected[^1].Metrics.EndedAtUtc,
                selected[^1].Identity.RunId)
            : null;
        var definitionTitles = selected.Length == 0
            ? EmptyProcessDefinitionTitles
            : await GetProcessDefinitionTitlesAsync(
                projectIds[0],
                cancellationToken);
        return new ProcessPage(
            selected
                .Select(record => MapProcessActivity(record, definitionTitles))
                .ToArray(),
            nextCursor);
    }

    private static IReadOnlyDictionary<Guid, string> EmptyProcessDefinitionTitles { get; } =
        new Dictionary<Guid, string>();

    private async Task<IReadOnlyDictionary<Guid, string>> GetProcessDefinitionTitlesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (processDefinitionTitlesByProject.TryGetValue(projectId, out var cachedTitles))
        {
            return cachedTitles;
        }

        var definitions = await processDefinitionCatalog.GetCompleteCatalogItemsAsync(
            ProcessWorkspaceShellScope.ForProject(projectId),
            cancellationToken: cancellationToken);
        var loadedTitles = definitions.ToDictionary(
            definition =>
                ProcessDefinitionCatalogProjectionService.CreateDefinitionId(definition.Key).Value,
            static definition => definition.Name);
        return processDefinitionTitlesByProject.GetOrAdd(projectId, loadedTitles);
    }

    private static ProjectManagerAgentSummaryInput MapAgentSummaryInput(
        AgentExecutionReportPage report)
        => new(
            report.Totals.KnownCostUsd,
            report.Totals.UnknownCostRunCount,
            report.Totals.LegacyProjectAttributionRunCount,
            report.Totals.InvalidProjectAttributionRunCount,
            report.Totals.InvalidCorrelationRunCount,
            report.DailyCostTrend
                .Select(static point => new ProjectManagerKnownExpensePoint(
                    point.DayUtc,
                    point.KnownCostUsd))
                .ToArray(),
            report.Items.Select(MapAgentActivity).ToArray());

    private static AgentExecutionReportQuery BuildAgentQuery(
        ProjectManagerSummaryScopeResolution scope,
        DateTimeOffset? historyFromUtc,
        DateTimeOffset asOfUtc,
        ProjectManagerActivityStatusFilter statusFilter,
        int pageIndex,
        int pageSize)
    {
        var query = new AgentExecutionReportQuery(
            ActivityFromUtc: historyFromUtc,
            ActivityToUtc: asOfUtc,
            PageIndex: pageIndex,
            PageSize: pageSize,
            ExcludeProcessCorrelatedRuns: true,
            ExcludeWorkflowCorrelatedRuns: true,
            ExcludeInvalidCorrelationRuns: true)
        {
            ProjectIds = scope.Scope ==
                ProjectManagerSummaryScope.UncategorizedAgentActivity
                    ? null
                    : scope.ProjectIds,
            UnattributedOnly = scope.Scope ==
                ProjectManagerSummaryScope.UncategorizedAgentActivity,
            DailyTrendFromUtc = historyFromUtc ??
                asOfUtc.AddDays(-MaximumChartDayCount)
        };
        return statusFilter switch
        {
            ProjectManagerActivityStatusFilter.All => query,
            ProjectManagerActivityStatusFilter.Active => query with
            {
                Statuses =
                [
                    AgentExecutionReportStatus.Active,
                    AgentExecutionReportStatus.Waiting
                ]
            },
            ProjectManagerActivityStatusFilter.Succeeded => query with
            {
                Statuses = [AgentExecutionReportStatus.Succeeded]
            },
            ProjectManagerActivityStatusFilter.Failed => query with
            {
                Statuses = [AgentExecutionReportStatus.Failed]
            },
            ProjectManagerActivityStatusFilter.Cancelled => query with
            {
                Statuses = [AgentExecutionReportStatus.Cancelled]
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(statusFilter),
                statusFilter,
                "The manager activity status filter is not supported.")
        };
    }

    private static IReadOnlyList<LlmChatOperationStatus> ResolveSimpleChatStatuses(
        ProjectManagerActivityStatusFilter statusFilter)
        => statusFilter switch
        {
            ProjectManagerActivityStatusFilter.All => [],
            ProjectManagerActivityStatusFilter.Active =>
            [
                LlmChatOperationStatus.Pending,
                LlmChatOperationStatus.Running,
                LlmChatOperationStatus.CancellationRequested,
                LlmChatOperationStatus.RecoveryRequired
            ],
            ProjectManagerActivityStatusFilter.Succeeded => [LlmChatOperationStatus.Succeeded],
            ProjectManagerActivityStatusFilter.Failed => [LlmChatOperationStatus.Failed],
            ProjectManagerActivityStatusFilter.Cancelled => [LlmChatOperationStatus.Cancelled],
            _ => throw new ArgumentOutOfRangeException(
                nameof(statusFilter),
                statusFilter,
                "The manager activity status filter is not supported.")
        };

    private static IReadOnlyList<WorkflowRunState> ResolveWorkflowStates(
        ProjectManagerActivityStatusFilter statusFilter)
        => statusFilter switch
        {
            ProjectManagerActivityStatusFilter.All => [],
            ProjectManagerActivityStatusFilter.Active =>
            [
                WorkflowRunState.NotStarted,
                WorkflowRunState.Running,
                WorkflowRunState.WaitingForInput,
                WorkflowRunState.Idle
            ],
            ProjectManagerActivityStatusFilter.Succeeded => [WorkflowRunState.Completed],
            ProjectManagerActivityStatusFilter.Failed => [WorkflowRunState.Failed],
            ProjectManagerActivityStatusFilter.Cancelled => [WorkflowRunState.Cancelled],
            _ => throw new ArgumentOutOfRangeException(
                nameof(statusFilter),
                statusFilter,
                "The manager activity status filter is not supported.")
        };

    private static ProcessRunDisposition? ResolveProcessDisposition(
        ProjectManagerActivityStatusFilter statusFilter)
        => statusFilter switch
        {
            ProjectManagerActivityStatusFilter.All => null,
            ProjectManagerActivityStatusFilter.Succeeded => ProcessRunDisposition.Succeeded,
            ProjectManagerActivityStatusFilter.Failed => ProcessRunDisposition.Failed,
            ProjectManagerActivityStatusFilter.Cancelled => ProcessRunDisposition.Cancelled,
            ProjectManagerActivityStatusFilter.Active => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(statusFilter),
                statusFilter,
                "The manager activity status filter is not supported.")
        };

    private static ProjectManagerActivity MapAgentActivity(ChatRunSummaryRecord run)
    {
        var durationMilliseconds = ToDurationMilliseconds(run.Duration);
        return new ProjectManagerActivity(
            new ProjectManagerActivityId(run.ExecutionRunId),
            ProjectManagerActivityKind.Conversation,
            string.IsNullOrWhiteSpace(run.Title)
                ? $"Conversation {run.ExecutionRunId:D}"
                : run.Title.Trim(),
            run.Summary.Trim(),
            MapAgentStatus(run),
            run.ActivityAtUtc,
            durationMilliseconds,
            run.KnownCostUsd,
            EstimatedCostUsd: 0m,
            run.HasUnknownCost,
            run.Tags);
    }

    private static ProjectManagerActivity MapSimpleChatActivity(
        LlmChatProjectStructureReportRun run)
    {
        var title = string.IsNullOrWhiteSpace(run.ConversationTitle)
            ? $"{run.DefinitionName} chat"
            : run.ConversationTitle.Trim();
        var summary = $"{run.DefinitionName} · Revision {run.DefinitionRevision:N0} · " +
            $"{run.ProviderName} / {run.Model}";
        return new ProjectManagerActivity(
            new ProjectManagerActivityId(run.OperationId.Value),
            ProjectManagerActivityKind.SimpleChat,
            title,
            summary,
            MapSimpleChatStatus(run.Status),
            run.ActivityAtUtc,
            run.DurationMilliseconds,
            run.KnownCostUsd,
            EstimatedCostUsd: 0m,
            run.HasUnknownCost,
            [
                $"simple-chat:{run.DefinitionId.Value:D}",
                $"provider:{run.ProviderName}",
                $"model:{run.Model}"
            ]);
    }

    private static ProjectManagerActivity MapWorkflowActivity(
        WorkflowProjectStructureReportRun run)
    {
        var title = string.IsNullOrWhiteSpace(run.Summary)
            ? $"Workflow {run.WorkflowId.Value:D}"
            : FirstLine(run.Summary);
        return new ProjectManagerActivity(
            new ProjectManagerActivityId(run.RunId.Value),
            ProjectManagerActivityKind.Workflow,
            title,
            run.Summary.Trim(),
            MapWorkflowStatus(run.State),
            run.ActivityAtUtc,
            run.DurationMilliseconds,
            run.KnownCostUsd,
            EstimatedCostUsd: 0m,
            run.HasUnknownCost,
            [
                $"workflow:{run.WorkflowId.Value:D}",
                $"backend:{run.Backend}"
            ]);
    }

    private static ProjectManagerActivity MapProcessActivity(
        ProcessRunRecordSummary run,
        IReadOnlyDictionary<Guid, string> definitionTitles)
    {
        var definitionId = run.Identity.DefinitionId?.Value;
        var title = definitionId.HasValue &&
                    definitionTitles.TryGetValue(definitionId.Value, out var definitionTitle)
            ? $"{definitionTitle} run"
            : definitionId.HasValue
                ? $"Process {definitionId.Value:D}"
                : $"Process run {run.Identity.RunId.Value:D}";
        var hasUnknownCost = run.FactsStatus != ProcessRunFactsStatus.Completed ||
            run.CompletenessWarnings.Contains(ProcessRunRecordWarningCode.MissingPricing);
        return new ProjectManagerActivity(
            new ProjectManagerActivityId(run.Identity.RunId.Value),
            ProjectManagerActivityKind.Process,
            title,
            BuildProcessHardFactSummary(run),
            MapProcessStatus(run.Disposition),
            run.Metrics.EndedAtUtc,
            run.Metrics.DurationMilliseconds ?? 0L,
            run.Metrics.ActualCost,
            run.Metrics.EstimatedCost,
            hasUnknownCost,
            [
                $"disposition:{run.Disposition}",
                $"completeness:{run.Completeness}"
            ]);
    }

    private static string BuildProcessHardFactSummary(ProcessRunRecordSummary run)
    {
        if (run.FactsStatus != ProcessRunFactsStatus.Completed)
        {
            return $"{run.Disposition}: hard facts are {run.FactsStatus}; detailed metrics are unavailable.";
        }

        var metrics = run.Metrics;
        return $"{run.Disposition}: {metrics.CompletedStepCount}/{metrics.ExecutableStepCount} executable steps completed; " +
            $"{metrics.FailedStepCount} failed, {metrics.CancelledStepCount} cancelled; " +
            $"{metrics.ExecutionCount} executions, {metrics.ReworkCount} reworks, " +
            $"{metrics.IncidentCount} incidents, {metrics.EscalationCount} escalations, " +
            $"{metrics.ArtifactCount} artifacts. Evidence: {run.Completeness}.";
    }

    private static ProjectManagerActivityStatus MapAgentStatus(ChatRunSummaryRecord run)
        => AgentExecutionReportStatusPolicy.Resolve(run.State, run.Outcome) switch
        {
            AgentExecutionReportStatus.Active => ProjectManagerActivityStatus.Active,
            AgentExecutionReportStatus.Waiting => ProjectManagerActivityStatus.Waiting,
            AgentExecutionReportStatus.Succeeded => ProjectManagerActivityStatus.Succeeded,
            AgentExecutionReportStatus.Failed => ProjectManagerActivityStatus.Failed,
            AgentExecutionReportStatus.Cancelled => ProjectManagerActivityStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(run),
                run.State,
                "The agent execution report status is not supported.")
        };

    private static ProjectManagerActivityStatus MapSimpleChatStatus(
        LlmChatOperationStatus status)
        => status switch
        {
            LlmChatOperationStatus.Pending => ProjectManagerActivityStatus.Active,
            LlmChatOperationStatus.Running => ProjectManagerActivityStatus.Active,
            LlmChatOperationStatus.CancellationRequested => ProjectManagerActivityStatus.Waiting,
            LlmChatOperationStatus.RecoveryRequired => ProjectManagerActivityStatus.Waiting,
            LlmChatOperationStatus.Succeeded => ProjectManagerActivityStatus.Succeeded,
            LlmChatOperationStatus.Failed => ProjectManagerActivityStatus.Failed,
            LlmChatOperationStatus.Cancelled => ProjectManagerActivityStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The Simple Chat operation status is not supported.")
        };

    private static ProjectManagerActivityStatus MapWorkflowStatus(WorkflowRunState state)
        => state switch
        {
            WorkflowRunState.NotStarted => ProjectManagerActivityStatus.Active,
            WorkflowRunState.Running => ProjectManagerActivityStatus.Active,
            WorkflowRunState.WaitingForInput => ProjectManagerActivityStatus.Waiting,
            WorkflowRunState.Idle => ProjectManagerActivityStatus.Waiting,
            WorkflowRunState.Completed => ProjectManagerActivityStatus.Succeeded,
            WorkflowRunState.Failed => ProjectManagerActivityStatus.Failed,
            WorkflowRunState.Cancelled => ProjectManagerActivityStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "The workflow run state is not supported.")
        };

    private static ProjectManagerActivityStatus MapProcessStatus(
        ProcessRunDisposition disposition)
        => disposition switch
        {
            ProcessRunDisposition.Succeeded => ProjectManagerActivityStatus.Succeeded,
            ProcessRunDisposition.Failed => ProjectManagerActivityStatus.Failed,
            ProcessRunDisposition.Cancelled => ProjectManagerActivityStatus.Cancelled,
            ProcessRunDisposition.Escalated => ProjectManagerActivityStatus.Escalated,
            ProcessRunDisposition.Blocked => ProjectManagerActivityStatus.Blocked,
            _ => throw new ArgumentOutOfRangeException(
                nameof(disposition),
                disposition,
                "The process disposition is not supported.")
        };

    private static ProjectManagerActivityPage EmptyActivityPage(
        ProjectManagerActivityPageRequest request)
        => new(
            [],
            request.PageIndex,
            request.PageSize,
            TotalCount: 0,
            new ProjectManagerCostTotals(0m, 0m, 0m, 0),
            TotalDurationMilliseconds: 0L);

    private static IReadOnlyList<Guid[]> ChunkProjectIds(IReadOnlyList<Guid> projectIds)
        => projectIds
            .Chunk(ProcessRunRecordPayloadLimits.MaximumProjectIdFilterCount)
            .ToArray();

    private static long ToDurationMilliseconds(TimeSpan? duration)
        => duration.HasValue
            ? ToDurationMilliseconds(duration.Value)
            : 0L;

    private static long ToDurationMilliseconds(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 0L;
        }

        return duration.TotalMilliseconds >= long.MaxValue
            ? long.MaxValue
            : (long)duration.TotalMilliseconds;
    }

    private static long SaturatingAdd(long left, long right)
        => right > long.MaxValue - left
            ? long.MaxValue
            : left + right;

    private static string FirstLine(string value)
    {
        var normalized = value.Trim();
        var lineBreakIndex = normalized.IndexOfAny(['\r', '\n']);
        return lineBreakIndex < 0
            ? normalized
            : normalized[..lineBreakIndex].Trim();
    }

    private static async ValueTask ReportProgressAsync(
        Func<ProjectManagerSummaryLoadProgress, ValueTask>? reportProgress,
        string stage,
        string message,
        int completedStageCount,
        int totalStageCount)
    {
        if (reportProgress is null)
        {
            return;
        }

        await reportProgress(new ProjectManagerSummaryLoadProgress(
            stage,
            message,
            completedStageCount,
            totalStageCount));
    }

    private static void ValidateScope(ProjectManagerSummaryScopeResolution scope)
    {
        if (scope.RootProjectId == Guid.Empty)
        {
            throw new ArgumentException("The manager summary root project id is required.", nameof(scope));
        }

        if (string.IsNullOrWhiteSpace(scope.RootProjectName))
        {
            throw new ArgumentException("The manager summary root project name is required.", nameof(scope));
        }

        if (!Enum.IsDefined(scope.Scope))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scope),
                scope.Scope,
                "The manager summary project scope is not supported.");
        }

        if (scope.Scope != ProjectManagerSummaryScope.UncategorizedAgentActivity &&
            (scope.ProjectIds.Count == 0 ||
             scope.ProjectIds.Any(static projectId => projectId == Guid.Empty)))
        {
            throw new ArgumentException(
                "A project manager summary scope must contain valid project identifiers.",
                nameof(scope));
        }

        if (scope.ProjectIds.Count != scope.ProjectIds.Distinct().Count())
        {
            throw new ArgumentException(
                "The manager summary scope cannot contain duplicate project identifiers.",
                nameof(scope));
        }
    }

    private static void ValidateOptions(ProjectManagerSummaryOptions options)
    {
        if (!Enum.IsDefined(options.ContentMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.ContentMode,
                "The manager summary content mode is not supported.");
        }

        if (!Enum.IsDefined(options.Scope))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Scope,
                "The manager summary project scope is not supported.");
        }

        if (!Enum.IsDefined(options.TimeRange))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.TimeRange,
                "The manager summary time range is not supported.");
        }
    }

    private static void ValidateActivityPageRequest(
        ProjectManagerActivityPageRequest request)
    {
        if (!Enum.IsDefined(request.Kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Kind,
                "The manager activity kind is not supported.");
        }

        if (!Enum.IsDefined(request.StatusFilter))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.StatusFilter,
                "The manager activity status filter is not supported.");
        }

        if (request.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageIndex,
                "The manager activity page index cannot be negative.");
        }

        if (request.PageSize is < 1 or > AgentExecutionReportQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageSize,
                $"The manager activity page size must be between 1 and {AgentExecutionReportQueryLimits.MaximumPageSize}.");
        }

        if (request.PageIndex > int.MaxValue / request.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageIndex,
                "The manager activity page offset is too large.");
        }

        var aggregate = request.KnownAggregate;
        if (aggregate is null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(aggregate.Totals);
        if (aggregate.TotalCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                aggregate.TotalCount,
                "The known manager activity count cannot be negative.");
        }

        if (aggregate.TotalDurationMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                aggregate.TotalDurationMilliseconds,
                "The known manager activity duration cannot be negative.");
        }

        var totals = aggregate.Totals;
        if (totals.HistoricalKnownUsd < 0m ||
            totals.HistoricalEstimatedUsd < 0m ||
            totals.FuturePlannedUsd < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                totals,
                "Known manager activity costs cannot be negative.");
        }

        if (totals.UnknownHistoricalCostCount < 0 ||
            totals.UnknownHistoricalCostCount > aggregate.TotalCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                totals.UnknownHistoricalCostCount,
                "The unknown cost count must be within the known manager activity count.");
        }
    }

    private sealed class ProcessReportAccumulator
    {
        private readonly SortedDictionary<DateOnly, ProcessExpenseAccumulator> dailyCost = [];
        private int runCount;
        private decimal actualCostUsd;
        private decimal estimatedCostUsd;
        private int unknownCostRunCount;
        private long durationMilliseconds;

        public void AddTotals(ProcessRunRecordAnalytics analytics)
        {
            runCount += analytics.MatchingRunCount;
            actualCostUsd += analytics.ActualCost;
            estimatedCostUsd += analytics.EstimatedCost;
            unknownCostRunCount += analytics.UnknownCostRunCount;
            durationMilliseconds = SaturatingAdd(
                durationMilliseconds,
                analytics.DurationMilliseconds);
        }

        public void AddTrend(IReadOnlyList<ProcessRunDailyCostTrendPoint> points)
        {
            foreach (var point in points)
            {
                if (!dailyCost.TryGetValue(point.DayUtc, out var expense))
                {
                    expense = new ProcessExpenseAccumulator();
                    dailyCost.Add(point.DayUtc, expense);
                }

                expense.HistoricalKnownUsd += point.ActualCost;
                expense.HistoricalEstimatedUsd += point.EstimatedCost;
            }
        }

        public ProjectManagerProcessSummaryInput Build()
            => new(
                runCount,
                actualCostUsd,
                estimatedCostUsd,
                unknownCostRunCount,
                durationMilliseconds,
                dailyCost
                    .Select(static item => new ProjectManagerProcessExpensePoint(
                        item.Key,
                        item.Value.HistoricalKnownUsd,
                        item.Value.HistoricalEstimatedUsd))
                    .ToArray(),
                Activities: []);
    }

    private sealed class ProcessExpenseAccumulator
    {
        public decimal HistoricalKnownUsd { get; set; }

        public decimal HistoricalEstimatedUsd { get; set; }
    }

    private sealed record ProcessPage(
        IReadOnlyList<ProjectManagerActivity> Items,
        ProcessRunRecordCursor? NextCursor)
    {
        public static ProcessPage Empty { get; } = new([], null);
    }

    private sealed record ProcessSummary(
        ProjectManagerProcessSummaryInput Report,
        ProcessPage Page)
    {
        public static ProcessSummary Empty { get; } = new(
            ProjectManagerProcessSummaryInput.Empty,
            ProcessPage.Empty);
    }
}
