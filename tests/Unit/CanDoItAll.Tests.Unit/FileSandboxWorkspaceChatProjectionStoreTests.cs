using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class FileSandboxWorkspaceChatProjectionStoreTests
{
    [Fact]
    public async Task Workspace_projection_reads_sessions_and_runs_for_one_agent_from_one_index_snapshot()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-chat-index-projection");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var projectionStore = new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var agentId = Guid.NewGuid();
            var otherAgentId = Guid.NewGuid();
            var selectedSession = CreateSessionSummary(agentId, "Selected thread");
            var otherSession = CreateSessionSummary(otherAgentId, "Other thread");
            var selectedRun = CreateRunSummary(agentId, selectedSession.Id);
            var otherRun = CreateRunSummary(otherAgentId, otherSession.Id);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                new ExecutionChatIndex(
                    "1.0",
                    Revision: 1,
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    SessionSummaries: [otherSession, selectedSession],
                    RunSummaries: [otherRun, selectedRun]),
                CancellationToken.None);

            var projection = await projectionStore.LoadChatWorkspaceProjectionAsync(
                agentId,
                CancellationToken.None);

            Assert.Equal(selectedSession, Assert.Single(projection.SessionSummaries));
            Assert.Equal(selectedRun, Assert.Single(projection.RunSummaries));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Missing_chat_index_is_rebuilt_once_from_canonical_records_and_persisted()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-chat-index-recovery");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var store = new FileSandboxWorkspaceStore(rootPath);
            var agentId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var session = new ChatSessionRecord(
                sessionId,
                agentId,
                "Recovered thread",
                now.AddMinutes(-2),
                now,
                Messages: [],
                LatestExecutionRunId: executionRunId);
            var run = CreateRun(
                executionRunId,
                agentId,
                sessionId,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                resultSummary: "Canonical recovery result");
            var historicalLog = new ExecutionLogEntry(
                Guid.NewGuid(),
                agentId,
                sessionId,
                now.AddSeconds(1),
                ExecutionState.Failed,
                "Historical log phase",
                "Historical log message")
            {
                ExecutionRunId = executionRunId
            };

            await jsonStore.WriteJsonAtomicallyAsync(layout.SessionPath(sessionId), session, CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(layout.RunPath(executionRunId), run, CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(
                Path.Combine(layout.RunLogsRoot(executionRunId), $"{historicalLog.Id:N}.json"),
                historicalLog,
                CancellationToken.None);

            var projections = await Task.WhenAll(
                Enumerable.Range(0, 4)
                    .Select(_ => store.LoadChatWorkspaceProjectionAsync(agentId)));

            Assert.True(File.Exists(layout.ExecutionChatIndexPath));
            var persistedIndex = await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                layout.ExecutionChatIndexPath,
                CancellationToken.None);
            var persistedRun = Assert.Single(Assert.IsType<ExecutionChatIndex>(persistedIndex).RunSummaries);
            Assert.Equal(ExecutionState.Completed, persistedRun.State);
            Assert.Equal("Run", persistedRun.Phase);
            Assert.Equal("Canonical recovery result", persistedRun.Message);
            Assert.All(projections, projection =>
            {
                Assert.Single(projection.SessionSummaries);
                Assert.Equal(persistedRun, Assert.Single(projection.RunSummaries));
            });
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Reporting_query_filters_project_runs_pages_and_aggregates_from_current_index_only()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-query");
        try
        {
            var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore(
                new FileSandboxWorkspaceJsonReadDiagnostics(reads.Add));
            var projectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var projectId = Guid.NewGuid();
            var processRunId = Guid.NewGuid();
            var now = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
            var latest = CreateReportingRunSummary(
                sourceKind: "project-structure",
                sourceId: projectId.ToString("D"),
                createdAtUtc: now.AddDays(-1),
                updatedAtUtc: now,
                state: ExecutionState.Completed,
                outcome: RunOutcome.Succeeded,
                knownCostUsd: 1.25m,
                duration: TimeSpan.FromMinutes(10),
                projectId: projectId);
            var olderActive = CreateReportingRunSummary(
                sourceKind: "workspace",
                sourceId: projectId.ToString("N"),
                createdAtUtc: now.AddDays(-2),
                updatedAtUtc: now.AddDays(-1),
                state: ExecutionState.Running,
                outcome: null,
                knownCostUsd: 0.75m,
                duration: TimeSpan.FromMinutes(20),
                hasUnknownCost: true,
                projectId: projectId);
            var processCorrelated = CreateReportingRunSummary(
                sourceKind: "project-structure",
                sourceId: projectId.ToString("D"),
                createdAtUtc: now.AddDays(-3),
                updatedAtUtc: now.AddHours(-1),
                state: ExecutionState.Running,
                outcome: null,
                knownCostUsd: 50m,
                duration: TimeSpan.FromHours(1),
                processRunId: processRunId.ToString("D"),
                projectId: projectId);
            var otherProject = CreateReportingRunSummary(
                sourceKind: "project-structure",
                sourceId: Guid.NewGuid().ToString("D"),
                createdAtUtc: now.AddDays(-1),
                updatedAtUtc: now.AddMinutes(-1),
                state: ExecutionState.Completed,
                outcome: RunOutcome.Succeeded,
                knownCostUsd: 100m,
                duration: TimeSpan.FromHours(2),
                projectId: Guid.NewGuid());
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                new ExecutionChatIndex(
                    "1.0",
                    Revision: 1,
                    UpdatedAtUtc: now,
                    SessionSummaries: [],
                    RunSummaries:
                    [
                        otherProject,
                        processCorrelated,
                        olderActive,
                        latest
                    ]),
                CancellationToken.None);
            reads.Clear();

            var report = await projectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery(
                    CreatedFromUtc: now.AddDays(-2),
                    CreatedToUtc: now,
                    ActivityFromUtc: now.AddDays(-1),
                    ActivityToUtc: now,
                    States:
                    [
                        ExecutionState.Preparing,
                        ExecutionState.Running,
                        ExecutionState.WaitingOnTool,
                        ExecutionState.Persisting,
                        ExecutionState.Completed
                    ],
                    PageIndex: 1,
                    PageSize: 1,
                    ExcludeProcessCorrelatedRuns: true)
                {
                    ProjectIds = [projectId]
                },
                CancellationToken.None);

            Assert.Equal(2, report.TotalCount);
            Assert.Equal(1, report.PageIndex);
            Assert.Equal(1, report.PageSize);
            Assert.Equal(olderActive.ExecutionRunId, Assert.Single(report.Items).ExecutionRunId);
            Assert.Equal(2m, report.Totals.KnownCostUsd);
            Assert.Equal(TimeSpan.FromMinutes(30), report.Totals.TotalDuration);
            Assert.Equal(1, report.Totals.UnknownCostRunCount);
            Assert.Equal(2, report.DailyCostTrend.Count);
            var read = Assert.Single(reads);
            Assert.Equal(
                Path.GetFullPath(layout.ExecutionChatIndexPath),
                read.FullPath,
                ignoreCase: true);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public void Reporting_projection_uses_recorded_scope_then_explicit_legacy_attribution()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-attribution-query");
        try
        {
            var projectId = Guid.NewGuid();
            var now = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
            var recordedRun = CreateRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                sessionId: null,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                sourceKind: "processes",
                sourceId: "workspace:global") with
            {
                MetadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
                    "{}",
                    WorkspaceScopeDescriptor.Project(projectId.ToString("D")))
            };
            var legacyRun = CreateRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                sessionId: null,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now.AddMinutes(-1),
                sourceKind: "processes-live",
                sourceId: $"live:project:{projectId:D}");
            var invalidRun = CreateRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                sessionId: null,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now.AddMinutes(-2),
                sourceKind: "project-structure",
                sourceId: "not-a-guid");
            var unattributedRun = CreateRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                sessionId: null,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now.AddMinutes(-3),
                sourceKind: "agents",
                sourceId: "overview");
            var recorded = WorkspaceChatProjectionBuilder
                .CreateChatRunSummary(recordedRun, []) with
            {
                ReportingProjectionVersion =
                    WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion
            };
            var legacy = WorkspaceChatProjectionBuilder
                .CreateChatRunSummary(legacyRun, []) with
            {
                ReportingProjectionVersion =
                    WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion
            };
            var invalid = WorkspaceChatProjectionBuilder
                .CreateChatRunSummary(invalidRun, []) with
            {
                ReportingProjectionVersion =
                    WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion
            };
            var unattributed = WorkspaceChatProjectionBuilder
                .CreateChatRunSummary(unattributedRun, []) with
            {
                ReportingProjectionVersion =
                    WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion
            };
            var store = new FileSandboxWorkspaceChatProjectionStore(
                new FileSandboxWorkspaceStorageLayout(rootPath),
                new FileSandboxWorkspaceJsonStore());
            var index = new ExecutionChatIndex(
                "1.0",
                Revision: 1,
                UpdatedAtUtc: now,
                SessionSummaries: [],
                RunSummaries: [recorded, legacy, invalid, unattributed],
                ReportingProjectionVersion:
                    WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion);

            var projectReport = store.QueryExecutionReport(
                index,
                new AgentExecutionReportQuery
                {
                    ProjectIds = [projectId]
                },
                CancellationToken.None);
            var unattributedReport = store.QueryExecutionReport(
                index,
                new AgentExecutionReportQuery
                {
                    UnattributedOnly = true
                },
                CancellationToken.None);

            Assert.Equal(projectId, recorded.ProjectId);
            Assert.Equal(
                AgentExecutionProjectAttributionSource.RecordedScope,
                recorded.ProjectAttributionSource);
            Assert.Equal(projectId, legacy.ProjectId);
            Assert.Equal(
                AgentExecutionProjectAttributionSource.LegacySource,
                legacy.ProjectAttributionSource);
            Assert.Equal(
                AgentExecutionProjectAttributionSource.InvalidLegacySource,
                invalid.ProjectAttributionSource);
            Assert.Equal(
                [recorded.ExecutionRunId, legacy.ExecutionRunId],
                projectReport.Items.Select(static item => item.ExecutionRunId));
            Assert.Equal(1, projectReport.Totals.LegacyProjectAttributionRunCount);
            Assert.Equal(1, projectReport.Totals.InvalidProjectAttributionRunCount);
            Assert.Equal(
                unattributed.ExecutionRunId,
                Assert.Single(unattributedReport.Items).ExecutionRunId);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public void Reporting_query_uses_activity_time_for_window_order_and_trend()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-activity-time-query");
        try
        {
            var now = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
            var completedOutsideWindow = CreateReportingRunSummary(
                "agents",
                "overview",
                now.AddDays(-3),
                now,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                10m,
                TimeSpan.FromHours(1)) with
            {
                CompletedAtUtc = now.AddDays(-2)
            };
            var activeInsideWindow = CreateReportingRunSummary(
                "agents",
                "overview",
                now.AddHours(-2),
                now.AddHours(-1),
                ExecutionState.Running,
                outcome: null,
                knownCostUsd: 1m,
                duration: TimeSpan.FromMinutes(30));
            var store = new FileSandboxWorkspaceChatProjectionStore(
                new FileSandboxWorkspaceStorageLayout(rootPath),
                new FileSandboxWorkspaceJsonStore());
            var index = new ExecutionChatIndex(
                "1.0",
                Revision: 1,
                UpdatedAtUtc: now,
                SessionSummaries: [],
                RunSummaries: [activeInsideWindow, completedOutsideWindow],
                ReportingProjectionVersion:
                    WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion);

            var report = store.QueryExecutionReport(
                index,
                new AgentExecutionReportQuery(
                    ActivityFromUtc: now.AddDays(-1),
                    ActivityToUtc: now),
                CancellationToken.None);

            Assert.Equal(
                activeInsideWindow.ExecutionRunId,
                Assert.Single(report.Items).ExecutionRunId);
            Assert.Equal(1m, report.Totals.KnownCostUsd);
            Assert.Equal(
                DateOnly.FromDateTime(activeInsideWindow.ActivityAtUtc.UtcDateTime),
                Assert.Single(report.DailyCostTrend).DayUtc);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Reporting_query_applies_plural_failure_state_and_outcome_filters()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-status");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var projectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var now = DateTimeOffset.UtcNow;
            var failed = CreateReportingRunSummary(
                "workspace",
                "project",
                now.AddHours(-3),
                now.AddHours(-2),
                ExecutionState.Failed,
                RunOutcome.Failed,
                1m,
                TimeSpan.FromSeconds(5));
            var completedFailure = CreateReportingRunSummary(
                "workspace",
                "project",
                now.AddHours(-2),
                now.AddHours(-1),
                ExecutionState.Completed,
                RunOutcome.Failed,
                2m,
                TimeSpan.FromSeconds(6));
            var cancelled = CreateReportingRunSummary(
                "workspace",
                "project",
                now.AddHours(-1),
                now,
                ExecutionState.Failed,
                RunOutcome.Cancelled,
                3m,
                TimeSpan.FromSeconds(7));
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                new ExecutionChatIndex(
                    "1.0",
                    Revision: 1,
                    UpdatedAtUtc: now,
                    SessionSummaries: [],
                    RunSummaries: [cancelled, completedFailure, failed]),
                CancellationToken.None);

            var report = await projectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery(
                    States: [ExecutionState.Failed, ExecutionState.Completed],
                    Outcomes: [RunOutcome.Failed]),
                CancellationToken.None);

            Assert.Equal(2, report.TotalCount);
            Assert.Equal(
                [completedFailure.ExecutionRunId, failed.ExecutionRunId],
                report.Items.Select(item => item.ExecutionRunId).ToArray());
            Assert.Equal(3m, report.Totals.KnownCostUsd);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Reporting_query_filters_with_the_same_normalized_status_used_by_manager_views()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-normalized-status");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var projectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var now = DateTimeOffset.UtcNow;
            var legacyCompleted = CreateReportingRunSummary(
                "workspace",
                "project",
                now.AddHours(-3),
                now.AddHours(-2),
                ExecutionState.Completed,
                null,
                1m,
                TimeSpan.FromSeconds(5));
            var contradictoryFailure = CreateReportingRunSummary(
                "workspace",
                "project",
                now.AddHours(-2),
                now.AddHours(-1),
                ExecutionState.Completed,
                RunOutcome.Failed,
                2m,
                TimeSpan.FromSeconds(6));
            var cancelled = CreateReportingRunSummary(
                "workspace",
                "project",
                now.AddHours(-1),
                now,
                ExecutionState.Failed,
                RunOutcome.Cancelled,
                3m,
                TimeSpan.FromSeconds(7));
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                new ExecutionChatIndex(
                    "1.0",
                    Revision: 1,
                    UpdatedAtUtc: now,
                    SessionSummaries: [],
                    RunSummaries: [cancelled, contradictoryFailure, legacyCompleted]),
                CancellationToken.None);

            var succeeded = await projectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery()
                {
                    Statuses = [AgentExecutionReportStatus.Succeeded]
                },
                CancellationToken.None);
            var failed = await projectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery()
                {
                    Statuses = [AgentExecutionReportStatus.Failed]
                },
                CancellationToken.None);
            var cancelledReport = await projectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery()
                {
                    Statuses = [AgentExecutionReportStatus.Cancelled]
                },
                CancellationToken.None);

            Assert.Equal(legacyCompleted.ExecutionRunId, Assert.Single(succeeded.Items).ExecutionRunId);
            Assert.Equal(contradictoryFailure.ExecutionRunId, Assert.Single(failed.Items).ExecutionRunId);
            Assert.Equal(cancelled.ExecutionRunId, Assert.Single(cancelledReport.Items).ExecutionRunId);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Reporting_totals_exclude_process_runs_and_daily_cost_trend_is_bounded()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-trend");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var projectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var processRunId = Guid.NewGuid().ToString("D");
            var summaries = new List<ChatRunSummaryRecord>();
            for (var day = 0; day < 370; day++)
            {
                var atUtc = start.AddDays(day);
                summaries.Add(CreateReportingRunSummary(
                    "workspace",
                    "project",
                    atUtc,
                    atUtc,
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    knownCostUsd: 1m,
                    duration: TimeSpan.FromMinutes(1),
                    hasUnknownCost: day == 10,
                    processRunId: day == 0 ? processRunId : string.Empty));
            }

            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                new ExecutionChatIndex(
                    "1.0",
                    Revision: 1,
                    UpdatedAtUtc: start.AddDays(369),
                    SessionSummaries: [],
                    RunSummaries: summaries),
                CancellationToken.None);

            var report = await projectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery(
                    PageSize: AgentExecutionReportQueryLimits.MaximumPageSize,
                    ExcludeProcessCorrelatedRuns: true),
                CancellationToken.None);

            Assert.Equal(369, report.TotalCount);
            Assert.Equal(369m, report.Totals.KnownCostUsd);
            Assert.Equal(TimeSpan.FromMinutes(369), report.Totals.TotalDuration);
            Assert.Equal(1, report.Totals.UnknownCostRunCount);
            Assert.Equal(
                AgentExecutionReportQueryLimits.MaximumDailyTrendDays,
                report.DailyCostTrend.Count);
            Assert.Equal(
                DateOnly.FromDateTime(start.AddDays(4).UtcDateTime),
                report.DailyCostTrend[0].DayUtc);
            Assert.Equal(
                DateOnly.FromDateTime(start.AddDays(369).UtcDateTime),
                report.DailyCostTrend[^1].DayUtc);

            var calendarBoundedReport = await projectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery(
                    PageSize: AgentExecutionReportQueryLimits.MaximumPageSize,
                    ExcludeProcessCorrelatedRuns: true)
                {
                    DailyTrendFromUtc = start.AddDays(360)
                },
                CancellationToken.None);

            Assert.Equal(369, calendarBoundedReport.TotalCount);
            Assert.Equal(10, calendarBoundedReport.DailyCostTrend.Count);
            Assert.Equal(
                DateOnly.FromDateTime(start.AddDays(360).UtcDateTime),
                calendarBoundedReport.DailyCostTrend[0].DayUtc);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Saving_run_detail_persists_enriched_reporting_fields_without_double_counting_cost()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-save");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var projectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var now = DateTimeOffset.UtcNow;
            var run = CreateRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                sessionId: null,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                resultSummary: "Prepared reporting result",
                sourceKind: "project-structure",
                sourceId: "project-42",
                processRunId: string.Empty,
                title: new string('T', 160));
            var metric = CreateMetric(run, now, costUsd: 99m);
            var detail = new ExecutionRunDetail(
                run,
                ChatSession: null,
                ExecutionLog: [],
                Metrics: [metric])
            {
                UsageObservations =
                [
                    CreateUsageObservation(
                        run,
                        now,
                        calculatedCostUsd: 0.30m,
                        providerCostUsd: 50m,
                        processRunId: "process-42",
                        workflowRunId: "workflow-b"),
                    CreateUsageObservation(
                        run,
                        now.AddSeconds(1),
                        calculatedCostUsd: null,
                        providerCostUsd: 0.20m,
                        processRunId: "process-42",
                        workflowRunId: "workflow-a"),
                    CreateUsageObservation(
                        run,
                        now.AddSeconds(2),
                        calculatedCostUsd: 0m,
                        providerCostUsd: null,
                        processRunId: "process-42",
                        workflowRunId: "WORKFLOW-A"),
                    CreateUsageObservation(
                        run,
                        now.AddSeconds(3),
                        calculatedCostUsd: 100m,
                        providerCostUsd: 200m,
                        processRunId: "process-42",
                        workflowRunId: "workflow-c",
                        usageStatus:
                            ProviderUsageObservationStatus.MissingAfterProviderActivity)
                ]
            };
            var executionIndex = new ExecutionStorageIndex(
                Version: "1.0",
                Revision: 1,
                UpdatedAtUtc: now,
                SessionCount: 0,
                RunCount: 1,
                LogCount: 0,
                MetricCount: 1,
                ApprovalCount: 0,
                ArtifactCount: 0,
                CheckpointCount: 0,
                ReceiptCount: 0,
                ActiveRunCount: 0,
                FailedRunCount: 0,
                UsageObservationCount: 4);

            await projectionStore.SaveRunDetailAsync(
                previousDetail: null,
                detail,
                executionIndex,
                CancellationToken.None);

            var persisted = Assert.IsType<ExecutionChatIndex>(
                await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    CancellationToken.None));
            var summary = Assert.Single(persisted.RunSummaries);
            Assert.Equal(
                WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion,
                summary.ReportingProjectionVersion);
            Assert.Equal(120, summary.Title.Length);
            Assert.EndsWith("...", summary.Title, StringComparison.Ordinal);
            Assert.Equal("Prepared reporting result", summary.Summary);
            Assert.Equal("project-structure", summary.SourceKind);
            Assert.Equal("project-42", summary.SourceId);
            Assert.Equal(["project-structure"], summary.Tags);
            Assert.Equal("process-42", summary.ProcessRunId);
            Assert.Equal("workflow-a", summary.WorkflowRunId);
            Assert.Equal(
                ["workflow-a", "workflow-b", "workflow-c"],
                summary.WorkflowRunIds);
            Assert.Equal(TimeSpan.FromMinutes(1), summary.Duration);
            Assert.Equal(50.20m, summary.KnownCostUsd);
            Assert.True(summary.HasUnknownCost);

            var knownFreeSummary =
                WorkspaceChatProjectionBuilder.CreateChatRunSummary(
                    detail with
                    {
                        Metrics = [],
                        UsageObservations = [detail.UsageObservations[2]]
                    });
            Assert.Equal(0m, knownFreeSummary.KnownCostUsd);
            Assert.False(knownFreeSummary.HasUnknownCost);

            var metricFallbackSummary =
                WorkspaceChatProjectionBuilder.CreateChatRunSummary(
                    detail with
                    {
                        Run = detail.Run with
                        {
                            StartedAtUtc = null
                        },
                        UsageObservations = []
                    });
            Assert.Equal(99m, metricFallbackSummary.KnownCostUsd);
            Assert.False(metricFallbackSummary.HasUnknownCost);
            Assert.Null(metricFallbackSummary.Duration);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Reporting_query_rebuilds_a_stale_chat_index_before_filtering()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-stale-index");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var projectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var now = DateTimeOffset.UtcNow;
            var firstRun = CreateRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                sessionId: null,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now.AddMinutes(-1));
            var secondRun = CreateRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                sessionId: null,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.RunPath(firstRun.Id),
                firstRun,
                CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.RunPath(secondRun.Id),
                secondRun,
                CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionIndexPath,
                new ExecutionStorageIndex(
                    Version: "1.0",
                    Revision: 2,
                    UpdatedAtUtc: now,
                    SessionCount: 0,
                    RunCount: 2,
                    LogCount: 0,
                    MetricCount: 0,
                    ApprovalCount: 0,
                    ArtifactCount: 0,
                    CheckpointCount: 0,
                    ReceiptCount: 0,
                    ActiveRunCount: 0,
                    FailedRunCount: 0),
                CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                new ExecutionChatIndex(
                    "1.0",
                    Revision: 1,
                    UpdatedAtUtc: now.AddMinutes(-1),
                    SessionSummaries: [],
                    RunSummaries:
                    [
                        WorkspaceChatProjectionBuilder.CreateChatRunSummary(
                            new ExecutionRunDetail(
                                firstRun,
                                ChatSession: null,
                                ExecutionLog: [],
                                Metrics: []))
                    ]),
                CancellationToken.None);

            var report = await projectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery(),
                CancellationToken.None);

            Assert.Equal(2, report.TotalCount);
            Assert.Equal(
                [secondRun.Id, firstRun.Id],
                report.Items.Select(item => item.ExecutionRunId).ToArray());
            var persisted = Assert.IsType<ExecutionChatIndex>(
                await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    CancellationToken.None));
            Assert.Equal(2, persisted.Revision);
            Assert.Equal(2, persisted.RunSummaries.Count);
            Assert.All(
                persisted.RunSummaries,
                summary => Assert.Equal(
                    WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion,
                    summary.ReportingProjectionVersion));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Legacy_chat_index_is_upgraded_only_by_first_reporting_query_and_then_served_from_index()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-legacy-upgrade");
        try
        {
            var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore(
                new FileSandboxWorkspaceJsonReadDiagnostics(reads.Add));
            var projectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var now = DateTimeOffset.UtcNow;
            var run = CreateRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                sessionId: null,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                resultSummary: "Legacy result",
                sourceKind: "project-structure",
                sourceId: "project-legacy");
            var legacySummary = CreateRunSummary(run.AgentId, Guid.NewGuid()) with
            {
                ExecutionRunId = run.Id,
                ChatSessionId = null,
                UpdatedAtUtc = run.UpdatedAtUtc,
                State = run.State,
                Outcome = run.Outcome
            };
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.RunPath(run.Id),
                run,
                CancellationToken.None);
            var usage = CreateUsageObservation(
                run,
                now,
                calculatedCostUsd: 0.40m,
                providerCostUsd: 10m,
                workflowRunId: "workflow-legacy");
            await jsonStore.WriteJsonAtomicallyAsync(
                Path.Combine(
                    layout.RunUsageRoot(run.Id),
                    $"{usage.Id:N}.json"),
                usage,
                CancellationToken.None);
            Directory.CreateDirectory(layout.ExecutionStorageRoot);
            var legacyJson = JsonSerializer.Serialize(
                new
                {
                    version = "1.0",
                    revision = 1,
                    updatedAtUtc = now,
                    sessionSummaries = Array.Empty<ChatSessionSummaryRecord>(),
                    runSummaries = new[]
                    {
                        new
                        {
                            executionRunId = legacySummary.ExecutionRunId,
                            agentId = legacySummary.AgentId,
                            chatSessionId = legacySummary.ChatSessionId,
                            updatedAtUtc = legacySummary.UpdatedAtUtc,
                            state = legacySummary.State,
                            phase = legacySummary.Phase,
                            message = legacySummary.Message,
                            outcome = legacySummary.Outcome
                        }
                    }
                },
                jsonStore.SerializerOptions);
            await File.WriteAllTextAsync(
                layout.ExecutionChatIndexPath,
                legacyJson);

            var ordinaryProjection =
                await projectionStore.LoadChatWorkspaceProjectionAsync(
                    run.AgentId,
                    CancellationToken.None);

            Assert.Equal(
                0,
                Assert.Single(ordinaryProjection.RunSummaries)
                    .ReportingProjectionVersion);
            var stillLegacy = Assert.IsType<ExecutionChatIndex>(
                await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    CancellationToken.None));
            Assert.Equal(
                0,
                Assert.Single(stillLegacy.RunSummaries)
                    .ReportingProjectionVersion);

            var firstReport = await projectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery(
                    SourceKinds: ["project-structure"],
                    SourceIds: ["PROJECT-LEGACY"]),
                CancellationToken.None);

            var upgraded = Assert.Single(firstReport.Items);
            Assert.Equal(
                WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion,
                upgraded.ReportingProjectionVersion);
            Assert.Equal(10m, upgraded.KnownCostUsd);
            Assert.Equal(["workflow-legacy"], upgraded.WorkflowRunIds);
            var persisted = Assert.IsType<ExecutionChatIndex>(
                await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    CancellationToken.None));
            Assert.Equal(
                WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion,
                Assert.Single(persisted.RunSummaries)
                    .ReportingProjectionVersion);

            reads.Clear();
            var restartedProjectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var secondReport = await restartedProjectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery(),
                CancellationToken.None);

            Assert.Single(secondReport.Items);
            var read = Assert.Single(reads);
            Assert.Equal(
                Path.GetFullPath(layout.ExecutionChatIndexPath),
                read.FullPath,
                ignoreCase: true);

            reads.Clear();
            var cachedReport = await restartedProjectionStore.QueryExecutionReportAsync(
                new AgentExecutionReportQuery(),
                CancellationToken.None);

            Assert.Single(cachedReport.Items);
            Assert.Empty(reads);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Cancelled_legacy_reporting_upgrade_does_not_publish_partial_checkpoints()
    {
        const int runCount = 60;
        const int cancelAfterRunReads = 52;
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-atomic-upgrade");
        try
        {
            using var cancellation = new CancellationTokenSource();
            var canonicalRunReadCount = 0;
            var diagnostics = new FileSandboxWorkspaceJsonReadDiagnostics(
                read =>
                {
                    if (read.PayloadType == typeof(ExecutionRunRecord) &&
                        Interlocked.Increment(ref canonicalRunReadCount) ==
                        cancelAfterRunReads)
                    {
                        cancellation.Cancel();
                    }
                });
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore(diagnostics);
            var projectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var now = DateTimeOffset.UtcNow;
            var legacySummaries = new List<ChatRunSummaryRecord>(runCount);
            for (var index = 0; index < runCount; index++)
            {
                var run = CreateRun(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    sessionId: null,
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    now.AddMinutes(-index),
                    resultSummary: $"Legacy result {index}");
                await jsonStore.WriteJsonAtomicallyAsync(
                    layout.RunPath(run.Id),
                    run,
                    CancellationToken.None);
                legacySummaries.Add(
                    CreateRunSummary(run.AgentId, Guid.NewGuid()) with
                    {
                        ExecutionRunId = run.Id,
                        ChatSessionId = null,
                        UpdatedAtUtc = run.UpdatedAtUtc,
                        State = run.State,
                        Outcome = run.Outcome,
                        ReportingProjectionVersion = 0
                    });
            }

            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                new ExecutionChatIndex(
                    "1.0",
                    Revision: 1,
                    UpdatedAtUtc: now,
                    SessionSummaries: [],
                    RunSummaries: legacySummaries,
                    ReportingProjectionVersion: 0),
                CancellationToken.None);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => projectionStore.QueryExecutionReportAsync(
                    new AgentExecutionReportQuery(),
                    cancellation.Token));

            var persisted = Assert.IsType<ExecutionChatIndex>(
                await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    CancellationToken.None));
            Assert.Equal(0, persisted.ReportingProjectionVersion);
            Assert.All(
                persisted.RunSummaries,
                static summary => Assert.Equal(
                    0,
                    summary.ReportingProjectionVersion));
            Assert.True(
                Volatile.Read(ref canonicalRunReadCount) >=
                cancelAfterRunReads);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Legacy_reporting_upgrade_does_not_overwrite_a_concurrent_index_change()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-upgrade-conflict");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var projectionStore =
                new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var now = DateTimeOffset.UtcNow;
            var run = CreateRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                sessionId: null,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.RunPath(run.Id),
                run,
                CancellationToken.None);
            var legacyIndex = new ExecutionChatIndex(
                "1.0",
                Revision: 1,
                UpdatedAtUtc: now,
                SessionSummaries: [],
                RunSummaries:
                [
                    CreateRunSummary(run.AgentId, Guid.NewGuid()) with
                    {
                        ExecutionRunId = run.Id,
                        ChatSessionId = null,
                        UpdatedAtUtc = run.UpdatedAtUtc,
                        State = run.State,
                        Outcome = run.Outcome,
                        ReportingProjectionVersion = 0
                    }
                ],
                ReportingProjectionVersion: 0);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                legacyIndex,
                CancellationToken.None);

            var preparation =
                await projectionStore.InspectExecutionReportIndexAsync(
                    CancellationToken.None);
            preparation =
                await projectionStore.MaterializeExecutionReportIndexAsync(
                    preparation,
                    CancellationToken.None);
            var concurrentIndex = legacyIndex with
            {
                Version = "1.0-concurrent",
                Revision = 2
            };
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                concurrentIndex,
                CancellationToken.None);

            var published =
                await projectionStore.TryPublishExecutionReportIndexAsync(
                    preparation,
                    CancellationToken.None);

            Assert.Null(published);
            var persisted = Assert.IsType<ExecutionChatIndex>(
                await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    CancellationToken.None));
            Assert.Equal("1.0-concurrent", persisted.Version);
            Assert.Equal(2, persisted.Revision);
            Assert.Equal(0, persisted.ReportingProjectionVersion);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Theory]
    [InlineData(-1, 25)]
    [InlineData(0, 0)]
    [InlineData(0, AgentExecutionReportQueryLimits.MaximumPageSize + 1)]
    [InlineData(int.MaxValue, AgentExecutionReportQueryLimits.MaximumPageSize)]
    public async Task Reporting_query_rejects_invalid_page_bounds(
        int pageIndex,
        int pageSize)
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-bounds");
        try
        {
            var projectionStore = new FileSandboxWorkspaceChatProjectionStore(
                new FileSandboxWorkspaceStorageLayout(rootPath),
                new FileSandboxWorkspaceJsonStore());

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => projectionStore.QueryExecutionReportAsync(
                    new AgentExecutionReportQuery(
                        PageIndex: pageIndex,
                        PageSize: pageSize),
                    CancellationToken.None));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Reporting_query_rejects_blank_source_filters_instead_of_broadening_scope()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-chat-reporting-source-bounds");
        try
        {
            var projectionStore = new FileSandboxWorkspaceChatProjectionStore(
                new FileSandboxWorkspaceStorageLayout(rootPath),
                new FileSandboxWorkspaceJsonStore());

            await Assert.ThrowsAsync<ArgumentException>(
                () => projectionStore.QueryExecutionReportAsync(
                    new AgentExecutionReportQuery(
                        SourceKinds: ["workspace", " "]),
                    CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(
                () => projectionStore.QueryExecutionReportAsync(
                    new AgentExecutionReportQuery(
                        SourceIds: [""]),
                    CancellationToken.None));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Execution_summary_rebuilds_and_persists_missing_index_without_loading_chat_projection()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-execution-index-recovery");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var store = new FileSandboxWorkspaceStore(rootPath);
            var agentId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var session = new ChatSessionRecord(
                sessionId,
                agentId,
                "Indexed thread",
                now.AddMinutes(-2),
                now,
                Messages: []);
            var activeRun = CreateRun(
                Guid.NewGuid(),
                agentId,
                sessionId,
                ExecutionState.Running,
                outcome: null,
                now);
            var failedRun = CreateRun(
                Guid.NewGuid(),
                agentId,
                sessionId,
                ExecutionState.Completed,
                RunOutcome.Failed,
                now.AddMinutes(-2));

            await jsonStore.WriteJsonAtomicallyAsync(layout.SessionPath(sessionId), session, CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(layout.RunPath(activeRun.Id), activeRun, CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(layout.RunPath(failedRun.Id), failedRun, CancellationToken.None);

            var summaries = await Task.WhenAll(
                Enumerable.Range(0, 4)
                    .Select(_ => store.LoadExecutionSummaryAsync()));

            Assert.All(summaries, summary =>
            {
                Assert.Equal(1, summary.SessionCount);
                Assert.Equal(1, summary.ActiveRuns);
                Assert.Equal(1, summary.FailedRuns);
            });
            Assert.True(File.Exists(layout.ExecutionIndexPath));
            Assert.False(File.Exists(layout.ExecutionChatIndexPath));

            var persistedIndex = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
                layout.ExecutionIndexPath,
                CancellationToken.None);
            var index = Assert.IsType<ExecutionStorageIndex>(persistedIndex);
            Assert.Equal(1, index.SessionCount);
            Assert.Equal(2, index.RunCount);
            Assert.Equal(1, index.ActiveRunCount);
            Assert.Equal(1, index.FailedRunCount);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Active_run_index_is_state_based_and_remains_delta_stable_across_old_run_updates()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-execution-index-active-invariant");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var store = new FileSandboxWorkspaceStore(rootPath);
            var agentId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var initialUpdatedAt = DateTimeOffset.UtcNow.AddHours(-3);
            var session = new ChatSessionRecord(
                sessionId,
                agentId,
                "Long-running thread",
                initialUpdatedAt.AddMinutes(-1),
                initialUpdatedAt,
                Messages: [],
                LatestExecutionRunId: executionRunId);
            var running = CreateRun(
                executionRunId,
                agentId,
                sessionId,
                ExecutionState.Running,
                outcome: null,
                initialUpdatedAt);

            await jsonStore.WriteJsonAtomicallyAsync(
                layout.SessionPath(sessionId),
                session,
                CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.RunPath(executionRunId),
                running,
                CancellationToken.None);

            var rebuilt = await store.LoadExecutionSummaryAsync();

            Assert.Equal(1, rebuilt.ActiveRuns);

            await store.UpdateExecutionRunDetailAsync(
                executionRunId,
                detail => detail with
                {
                    Run = detail.Run with
                    {
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    }
                });
            var afterActiveUpdate = await store.LoadExecutionSummaryAsync();

            Assert.Equal(1, afterActiveUpdate.ActiveRuns);

            await store.UpdateExecutionRunDetailAsync(
                executionRunId,
                detail =>
                {
                    var completedAtUtc = DateTimeOffset.UtcNow;
                    return detail with
                    {
                        Run = detail.Run with
                        {
                            State = ExecutionState.Completed,
                            Outcome = RunOutcome.Succeeded,
                            UpdatedAtUtc = completedAtUtc,
                            CompletedAtUtc = completedAtUtc
                        }
                    };
                });
            var afterCompletion = await store.LoadExecutionSummaryAsync();

            Assert.Equal(0, afterCompletion.ActiveRuns);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    private static ChatSessionSummaryRecord CreateSessionSummary(Guid agentId, string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new ChatSessionSummaryRecord(
            Guid.NewGuid(),
            agentId,
            title,
            now,
            now,
            MessageCount: 0,
            LastMessagePreview: "No messages yet.",
            PendingApprovalCount: 0,
            AutoApprovePendingToolCalls: false);
    }

    private static ChatRunSummaryRecord CreateRunSummary(Guid agentId, Guid sessionId)
        => new(
            Guid.NewGuid(),
            agentId,
            sessionId,
            DateTimeOffset.UtcNow,
            ExecutionState.Completed,
            "Completed",
            "Done",
            RunOutcome.Succeeded);

    private static ChatRunSummaryRecord CreateReportingRunSummary(
        string sourceKind,
        string sourceId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        ExecutionState state,
        RunOutcome? outcome,
        decimal knownCostUsd,
        TimeSpan duration,
        bool hasUnknownCost = false,
        string processRunId = "",
        Guid? projectId = null)
    {
        var correlatedProcessRunId =
            Guid.TryParse(processRunId, out var parsedProcessRunId) &&
            parsedProcessRunId != Guid.Empty
                ? parsedProcessRunId
                : (Guid?)null;
        return new ChatRunSummaryRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ChatSessionId: null,
            updatedAtUtc,
            state,
            Phase: "Run",
            Message: "Reporting run",
            outcome)
        {
            Title = "Reporting run",
            Summary = "Reporting summary",
            SourceKind = sourceKind,
            SourceId = sourceId,
            ProjectId = projectId,
            ProjectAttributionSource = projectId.HasValue
                ? AgentExecutionProjectAttributionSource.RecordedScope
                : AgentExecutionProjectAttributionSource.None,
            ProcessRunId = processRunId,
            CorrelatedProcessRunId = correlatedProcessRunId,
            InvalidCorrelationIdCount =
                string.IsNullOrWhiteSpace(processRunId) ||
                correlatedProcessRunId.HasValue
                    ? 0
                    : 1,
            CreatedAtUtc = createdAtUtc,
            StartedAtUtc = createdAtUtc,
            CompletedAtUtc = state is ExecutionState.Completed or ExecutionState.Failed
                ? updatedAtUtc
                : null,
            Duration = duration,
            KnownCostUsd = knownCostUsd,
            HasUnknownCost = hasUnknownCost,
            ReportingProjectionVersion =
                WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion
        };
    }

    private static ExecutionRunRecord CreateRun(
        Guid executionRunId,
        Guid agentId,
        Guid? sessionId,
        ExecutionState state,
        RunOutcome? outcome,
        DateTimeOffset updatedAt,
        string resultSummary = "Done",
        string sourceKind = "test",
        string? sourceId = null,
        string processRunId = "",
        string title = "Recovered run")
    {
        return new ExecutionRunRecord(
            executionRunId,
            agentId,
            sessionId,
            title,
            SourceKind: sourceKind,
            SourceId: sourceId ?? executionRunId.ToString("N"),
            CorrelationId: executionRunId.ToString("N"),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Recover indexes",
            resultSummary,
            ProviderName: "test",
            Model: "test",
            state,
            outcome,
            CreatedAtUtc: updatedAt.AddMinutes(-1),
            updatedAt,
            StartedAtUtc: updatedAt.AddMinutes(-1),
            CompletedAtUtc: state == ExecutionState.Completed ? updatedAt : null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: processRunId);
    }

    private static AgentRunMetric CreateMetric(
        ExecutionRunRecord run,
        DateTimeOffset createdAtUtc,
        decimal costUsd)
    {
        return new AgentRunMetric(
            Guid.NewGuid(),
            run.AgentId,
            run.ChatSessionId,
            createdAtUtc,
            run.Outcome ?? RunOutcome.Succeeded,
            run.ProviderName,
            run.Model,
            DurationMs: 60_000,
            InputTokens: 10,
            OutputTokens: 5,
            ToolCalls: 0)
        {
            ExecutionRunId = run.Id,
            CostUsd = costUsd
        };
    }

    private static ProviderUsageObservation CreateUsageObservation(
        ExecutionRunRecord run,
        DateTimeOffset createdAtUtc,
        decimal? calculatedCostUsd,
        decimal? providerCostUsd,
        string processRunId = "",
        string workflowRunId = "",
        ProviderUsageObservationStatus usageStatus =
            ProviderUsageObservationStatus.Observed)
    {
        return new ProviderUsageObservation(
            Guid.NewGuid(),
            createdAtUtc,
            run.ProviderName,
            ProviderKind.OpenAi,
            run.Model,
            ProviderTransportKind.Responses,
            ProviderUsageSourcePhases.AgentRuntime,
            usageStatus,
            InputTokens: 10,
            CachedInputTokens: 0,
            OutputTokens: 5,
            ReasoningTokens: 0,
            TotalTokens: 15,
            ToolCallCount: 0)
        {
            ExecutionRunId = run.Id,
            AgentId = run.AgentId,
            ChatSessionId = run.ChatSessionId,
            ProcessRunId = processRunId,
            WorkflowRunId = workflowRunId,
            CalculatedCostUsd = calculatedCostUsd,
            ProviderCostUsd = providerCostUsd
        };
    }
}
