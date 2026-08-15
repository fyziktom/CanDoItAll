using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectManagerSummaryWorkflowQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Known_aggregate_requests_only_the_workflow_page_and_is_returned_unchanged()
    {
        var projectId = Guid.NewGuid();
        var runId = WorkflowRunId.New();
        var reportStore = new RecordingWorkflowProjectStructureReportStore(
            new WorkflowProjectStructureReport(
                [
                    new WorkflowProjectStructureReportRun(
                        runId,
                        WorkflowId.New(),
                        WorkflowRunState.Failed,
                        WorkflowRuntimeBackendKind.InProcess,
                        "Failed workflow",
                        Now.AddMinutes(-1),
                        DurationMilliseconds: 60_000,
                        KnownCostUsd: 0.25m,
                        HasUnknownCost: false)
                ],
                PageIndex: 1,
                PageSize: 2,
                TotalCount: 0,
                KnownCostUsd: 0m,
                UnknownCostRunCount: 0,
                TotalDurationMilliseconds: 0L,
                DailyCost: []));
        var service = new ProjectManagerSummaryQueryService(
            planAnalytics: null!,
            agentWorkspace: null!,
            workflowProjectStructureReportStore: reportStore,
            processRunRecordStore: null!,
            processDefinitionCatalog:
                new ProcessDefinitionCatalogProjectionService(
                    new SystemProcessProjectionClock()),
            clock: null!);
        var knownTotals = new ProjectManagerCostTotals(
            HistoricalKnownUsd: 9.50m,
            HistoricalEstimatedUsd: 0m,
            FuturePlannedUsd: 0m,
            UnknownHistoricalCostCount: 3);
        var knownAggregate = new ProjectManagerActivityAggregate(
            TotalCount: 7,
            Totals: knownTotals,
            TotalDurationMilliseconds: 123_456L);
        var summary = CreateSummary(projectId);

        var page = await service.QueryActivityPageAsync(
            new ProjectManagerActivityPageRequest(
                summary,
                ProjectManagerActivityKind.Workflow,
                ProjectManagerActivityStatusFilter.Failed,
                PageIndex: 1,
                PageSize: 2)
            {
                KnownAggregate = knownAggregate
            });

        var query = Assert.Single(reportStore.Queries);
        Assert.False(query.IncludeAggregate);
        Assert.Equal([projectId], query.ProjectIds);
        Assert.Equal([WorkflowRunState.Failed], query.States);
        Assert.Equal(summary.HistoryFromUtc, query.ActivityFromUtc);
        Assert.Equal(summary.AsOfUtc, query.ActivityToUtc);
        Assert.Equal(1, query.PageIndex);
        Assert.Equal(2, query.PageSize);
        Assert.Equal(knownAggregate.TotalCount, page.TotalCount);
        Assert.Same(knownAggregate.Totals, page.Totals);
        Assert.Equal(knownAggregate.TotalDurationMilliseconds, page.TotalDurationMilliseconds);
        Assert.Equal(runId.Value, Assert.Single(page.Items).Id.Value);
    }

    private static ProjectManagerSummarySnapshot CreateSummary(Guid projectId)
    {
        var options = new ProjectManagerSummaryOptions();
        var scope = new ProjectManagerSummaryScopeResolution(
            projectId,
            "Project",
            ProjectManagerSummaryScope.CurrentProject,
            [projectId],
            DescendantCount: 0,
            RequiresConfirmation: false);
        return new ProjectManagerSummarySnapshot(
            projectId,
            "Project",
            options,
            scope,
            HistoryFromUtc: Now.AddDays(-7),
            AsOfUtc: Now,
            GeneratedAtUtc: Now,
            new ProjectManagerTaskSchedule(
                TaskCount: 0,
                StartUtc: null,
                EndUtc: null,
                DeliveryLeadTimeHours: null,
                ScheduledTaskDurationHours: 0m),
            new ProjectManagerCostTotals(0m, 0m, 0m, 0),
            CostBreakdown: [],
            OtherCurrencyFutureCosts: [],
            ExpenseTrend: [],
            LatestActivities: [],
            Warnings: []);
    }

    private sealed class RecordingWorkflowProjectStructureReportStore(
        WorkflowProjectStructureReport result) : IWorkflowProjectStructureReportStore
    {
        public List<WorkflowProjectStructureReportQuery> Queries { get; } = [];

        public Task<WorkflowProjectStructureReport> QueryProjectStructureReportAsync(
            WorkflowProjectStructureReportQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(result);
        }
    }
}
