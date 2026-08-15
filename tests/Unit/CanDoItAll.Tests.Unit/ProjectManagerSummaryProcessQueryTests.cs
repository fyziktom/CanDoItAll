using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectManagerSummaryProcessQueryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 22, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Initial_load_serializes_queries_that_share_the_process_store()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(ProjectsModuleAssemblyMarker).Assembly,
            typeof(WorkbenchModuleAssemblyMarker).Assembly
        ]);
        var databaseOptions = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"manager-summary-process-{Guid.NewGuid():N}")
            .Options;
        var projectId = Guid.NewGuid();
        await using (var dbContext = new AppDbContext(databaseOptions))
        {
            dbContext.Set<Project>().Add(new Project
            {
                Id = projectId,
                Name = "Manager summary",
                Slug = $"manager-summary-{projectId:N}"
            });
            await dbContext.SaveChangesAsync();
        }

        var processStore = new RecordingProcessRunRecordStore(
            supportsAnalytics: true,
            operationDelay: TimeSpan.FromMilliseconds(25));
        var service = new ProjectManagerSummaryQueryService(
            new ProjectPlanAnalyticsQueryService(
                new TestDbContextFactory(databaseOptions),
                new NoopProjectPartyIntegrationBridge(),
                new ProjectPlanSummaryCalculator()),
            new EmptyAgentExecutionReportReader(),
            new EmptyWorkflowProjectStructureReportStore(),
            processStore,
            CreateProcessDefinitionCatalog(),
            new FixedClock(Now));
        var scope = new ProjectManagerSummaryScopeResolution(
            projectId,
            "Manager summary",
            ProjectManagerSummaryScope.CurrentProject,
            [projectId],
            DescendantCount: 0,
            RequiresConfirmation: false);

        var result = await service.LoadAsync(
            scope,
            new ProjectManagerSummaryOptions(
                ProjectManagerSummaryContentMode.HistoryOnly,
                ProjectManagerSummaryScope.CurrentProject,
                ProjectManagerSummaryTimeRange.Month));

        Assert.Equal(projectId, result.ProjectId);
        Assert.False(processStore.DetectedOverlap);
        Assert.Single(processStore.AnalyticsQueries);
        Assert.Single(processStore.ListQueries);
    }

    [Fact]
    public async Task Known_aggregate_requests_only_the_process_page_and_is_returned_unchanged()
    {
        var projectId = Guid.NewGuid();
        var cursor = new ProcessRunRecordCursor(
            Now.AddMinutes(-10),
            ProcessRunId.New());
        var processStore = new RecordingProcessRunRecordStore();
        var service = new ProjectManagerSummaryQueryService(
            planAnalytics: null!,
            agentWorkspace: null!,
            workflowProjectStructureReportStore: null!,
            processRunRecordStore: processStore,
            processDefinitionCatalog: CreateProcessDefinitionCatalog(),
            clock: null!);
        var knownTotals = new ProjectManagerCostTotals(
            HistoricalKnownUsd: 9.50m,
            HistoricalEstimatedUsd: 2.25m,
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
                ProjectManagerActivityKind.Process,
                ProjectManagerActivityStatusFilter.Failed,
                PageIndex: 1,
                PageSize: 2)
            {
                KnownAggregate = knownAggregate,
                ProcessCursor = cursor
            });

        Assert.Empty(processStore.AnalyticsQueries);
        var query = Assert.Single(processStore.ListQueries);
        Assert.Equal([projectId], query.ProjectIds);
        Assert.Equal(ProcessRunDisposition.Failed, query.Disposition);
        Assert.True(query.RootRunsOnly);
        Assert.Equal(summary.HistoryFromUtc, query.EndedFromUtc);
        Assert.Equal(summary.AsOfUtc, query.EndedBeforeUtc);
        Assert.Equal(cursor, query.Cursor);
        Assert.Equal(2, query.Take);
        Assert.Equal(knownAggregate.TotalCount, page.TotalCount);
        Assert.Same(knownAggregate.Totals, page.Totals);
        Assert.Equal(
            knownAggregate.TotalDurationMilliseconds,
            page.TotalDurationMilliseconds);
    }

    [Fact]
    public async Task Invalid_known_aggregate_is_rejected_before_querying_a_store()
    {
        var projectId = Guid.NewGuid();
        var processStore = new RecordingProcessRunRecordStore();
        var service = new ProjectManagerSummaryQueryService(
            planAnalytics: null!,
            agentWorkspace: null!,
            workflowProjectStructureReportStore: null!,
            processRunRecordStore: processStore,
            processDefinitionCatalog: CreateProcessDefinitionCatalog(),
            clock: null!);
        var request = new ProjectManagerActivityPageRequest(
            CreateSummary(projectId),
            ProjectManagerActivityKind.Process,
            ProjectManagerActivityStatusFilter.All)
        {
            KnownAggregate = new ProjectManagerActivityAggregate(
                TotalCount: 1,
                new ProjectManagerCostTotals(0m, 0m, 0m, 2),
                TotalDurationMilliseconds: 0L)
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.QueryActivityPageAsync(request));

        Assert.Empty(processStore.AnalyticsQueries);
        Assert.Empty(processStore.ListQueries);
    }

    [Fact]
    public async Task Process_activity_uses_the_catalog_display_name()
    {
        var projectId = Guid.NewGuid();
        var definitionId = ProcessDefinitionCatalogProjectionService.CreateDefinitionId(
            new ProcessDefinitionCatalogItemKey("release-readiness-and-deployment"));
        var processStore = new RecordingProcessRunRecordStore(
            records: [CreateRecordSummary(projectId, definitionId)]);
        var service = new ProjectManagerSummaryQueryService(
            planAnalytics: null!,
            agentWorkspace: null!,
            workflowProjectStructureReportStore: null!,
            processRunRecordStore: processStore,
            processDefinitionCatalog: CreateProcessDefinitionCatalog(),
            clock: null!);
        var knownAggregate = new ProjectManagerActivityAggregate(
            TotalCount: 1,
            new ProjectManagerCostTotals(1.25m, 0m, 0m, 0),
            TotalDurationMilliseconds: 60_000L);

        var page = await service.QueryActivityPageAsync(
            new ProjectManagerActivityPageRequest(
                CreateSummary(projectId),
                ProjectManagerActivityKind.Process,
                ProjectManagerActivityStatusFilter.All)
            {
                KnownAggregate = knownAggregate
            });

        var activity = Assert.Single(page.Items);
        Assert.Equal("Release readiness and deployment control run", activity.Title);
        Assert.Equal(ProjectManagerActivityKind.Process, activity.Kind);
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

    private static ProcessDefinitionCatalogProjectionService CreateProcessDefinitionCatalog()
        => new(new SystemProcessProjectionClock());

    private sealed class RecordingProcessRunRecordStore(
        bool supportsAnalytics = false,
        TimeSpan operationDelay = default,
        IReadOnlyList<ProcessRunRecordSummary>? records = null) : IProcessRunRecordStore
    {
        private int activeOperationCount;

        public List<ProcessRunRecordListQuery> ListQueries { get; } = [];

        public List<ProcessRunRecordAnalyticsQuery> AnalyticsQueries { get; } = [];

        public bool DetectedOverlap { get; private set; }

        public Task<ProcessRunRecordPage> ListAsync(
            ProcessRunRecordListQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListQueries.Add(query);
            return ExecuteExclusiveAsync(
                new ProcessRunRecordPage(records ?? [], NextCursor: null),
                cancellationToken);
        }

        public Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
            ProcessRunRecordAnalyticsQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AnalyticsQueries.Add(query);
            return supportsAnalytics
                ? ExecuteExclusiveAsync(EmptyAnalytics(), cancellationToken)
                : throw new InvalidOperationException(
                    "A known manager-summary aggregate must bypass process analytics.");
        }

        public Task<bool> UpsertSeedAsync(
            ProcessRunRecordSeed seed,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> SupersedeAsync(
            ProcessRunRecordSupersession supersession,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProcessRunRecord?> GetAsync(
            ProcessRunId runId,
            bool includeSuperseded = false,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> CompleteFactsAsync(
            ProcessRunFactsCompletion completion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> FailFactsAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesAsync(
            ProcessRunRecordClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> CompleteNarrativeAsync(
            ProcessRunNarrativeCompletion completion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> FailNarrativeAsync(
            ProcessRunStageFailure failure,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private async Task<T> ExecuteExclusiveAsync<T>(
            T result,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref activeOperationCount) != 1)
            {
                DetectedOverlap = true;
                Interlocked.Decrement(ref activeOperationCount);
                throw new InvalidOperationException(
                    "Concurrent operations used the same process store.");
            }

            try
            {
                if (operationDelay > TimeSpan.Zero)
                {
                    await Task.Delay(operationDelay, cancellationToken);
                }

                return result;
            }
            finally
            {
                Interlocked.Decrement(ref activeOperationCount);
            }
        }

        private static ProcessRunRecordAnalytics EmptyAnalytics()
            => new(
                MatchingRunCount: 0,
                FactsAvailableRunCount: 0,
                EvidenceCompleteRunCount: 0,
                EvidencePartialRunCount: 0,
                FactsUnavailableRunCount: 0,
                LatestEndedAtUtc: null,
                MaximumSourceGlobalSequence: null,
                DurationMilliseconds: 0L,
                InputTokenCount: 0L,
                CachedInputTokenCount: 0L,
                OutputTokenCount: 0L,
                ReasoningTokenCount: 0L,
                TotalTokenCount: 0L,
                EstimatedCost: 0m,
                ActualCost: 0m,
                RepetitionCount: 0,
                ExecutionCount: 0,
                ReworkCount: 0,
                IncidentCount: 0,
                EscalationCount: 0,
                ToolCallCount: 0,
                ArtifactCount: 0,
                Dispositions: []);
    }

    private static ProcessRunRecordSummary CreateRecordSummary(
        Guid projectId,
        ProcessDefinitionId definitionId)
    {
        var runId = ProcessRunId.New();
        return new ProcessRunRecordSummary(
            new ProcessRunRecordIdentity(
                runId,
                runId,
                null,
                null,
                definitionId,
                null,
                projectId),
            ProcessRunDisposition.Succeeded,
            ProcessRunRecordLifecycleState.Current,
            ProcessRunRecordCompleteness.Complete,
            ProcessRunEvidenceSource.All,
            ProcessRunEvidenceSource.None,
            [],
            ProcessRunFactsStatus.Completed,
            1,
            null,
            null,
            null,
            ProcessRunNarrativeStatus.Completed,
            1,
            null,
            null,
            null,
            new ProcessRunRecordMetrics(
                Now.AddMinutes(-1),
                Now,
                60_000L,
                1,
                1,
                1,
                0,
                0,
                0,
                1,
                0,
                0,
                0,
                1_000,
                100,
                250,
                50,
                1_300,
                1.25m,
                1.25m,
                3,
                1,
                0),
            [],
            null,
            1,
            1,
            ProcessRunRecordSchema.CurrentVersion,
            Now);
    }

    private sealed class EmptyAgentExecutionReportReader : IAgentExecutionReportReader
    {
        public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
            AgentExecutionReportQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AgentExecutionReportPage(
                Items: [],
                PageIndex: query.PageIndex,
                PageSize: query.PageSize,
                new AgentExecutionReportTotals(
                    RunCount: 0,
                    KnownCostUsd: 0m,
                    TotalDuration: TimeSpan.Zero,
                    UnknownCostRunCount: 0),
                DailyCostTrend: []));
        }
    }

    private sealed class EmptyWorkflowProjectStructureReportStore :
        IWorkflowProjectStructureReportStore
    {
        public Task<WorkflowProjectStructureReport> QueryProjectStructureReportAsync(
            WorkflowProjectStructureReportQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WorkflowProjectStructureReport(
                Runs: [],
                query.PageIndex,
                query.PageSize,
                TotalCount: 0,
                KnownCostUsd: 0m,
                UnknownCostRunCount: 0,
                TotalDurationMilliseconds: 0L,
                DailyCost: []));
        }
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
