using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectManagerSummaryProcessQueryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 22, 16, 0, 0, TimeSpan.Zero);

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

    private sealed class RecordingProcessRunRecordStore : IProcessRunRecordStore
    {
        public List<ProcessRunRecordListQuery> ListQueries { get; } = [];

        public List<ProcessRunRecordAnalyticsQuery> AnalyticsQueries { get; } = [];

        public Task<ProcessRunRecordPage> ListAsync(
            ProcessRunRecordListQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListQueries.Add(query);
            return Task.FromResult(new ProcessRunRecordPage([], NextCursor: null));
        }

        public Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
            ProcessRunRecordAnalyticsQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AnalyticsQueries.Add(query);
            throw new InvalidOperationException(
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
    }
}
