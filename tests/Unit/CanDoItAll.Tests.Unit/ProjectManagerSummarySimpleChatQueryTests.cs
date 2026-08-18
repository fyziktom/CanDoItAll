using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectManagerSummarySimpleChatQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Known_aggregate_requests_only_the_simple_chat_page_and_maps_waiting_activity()
    {
        var projectId = Guid.NewGuid();
        var operationId = LlmChatOperationId.New();
        var definitionId = LlmChatDefinitionId.New();
        var reportStore = new RecordingLlmChatProjectStructureReportStore(
            new LlmChatProjectStructureReport(
                [
                    new LlmChatProjectStructureReportRun(
                        operationId,
                        LlmChatConversationId.New(),
                        definitionId,
                        DefinitionRevision: 3,
                        LlmChatOperationStatus.CancellationRequested,
                        "Architecture review",
                        "Senior C#",
                        "OpenAI",
                        "gpt-test",
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
            simpleChatProjectStructureReportStore: reportStore,
            workflowProjectStructureReportStore: null!,
            processRunRecordStore: null!,
            processDefinitionCatalog: null!,
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
                ProjectManagerActivityKind.SimpleChat,
                ProjectManagerActivityStatusFilter.Active,
                PageIndex: 1,
                PageSize: 2)
            {
                KnownAggregate = knownAggregate
            });

        var query = Assert.Single(reportStore.Queries);
        Assert.False(query.IncludeAggregate);
        Assert.Equal([projectId], query.ProjectIds);
        Assert.Equal(
            [
                LlmChatOperationStatus.Pending,
                LlmChatOperationStatus.Running,
                LlmChatOperationStatus.CancellationRequested,
                LlmChatOperationStatus.RecoveryRequired
            ],
            query.Statuses);
        Assert.Equal(summary.HistoryFromUtc, query.ActivityFromUtc);
        Assert.Equal(summary.AsOfUtc, query.ActivityToUtc);
        Assert.Equal(1, query.PageIndex);
        Assert.Equal(2, query.PageSize);
        Assert.Equal(knownAggregate.TotalCount, page.TotalCount);
        Assert.Same(knownAggregate.Totals, page.Totals);
        Assert.Equal(knownAggregate.TotalDurationMilliseconds, page.TotalDurationMilliseconds);
        var activity = Assert.Single(page.Items);
        Assert.Equal(operationId.Value, activity.Id.Value);
        Assert.Equal(ProjectManagerActivityKind.SimpleChat, activity.Kind);
        Assert.Equal(ProjectManagerActivityStatus.Waiting, activity.Status);
        Assert.Equal("Architecture review", activity.Title);
        Assert.Contains("Senior C#", activity.Summary, StringComparison.Ordinal);
        Assert.Contains($"simple-chat:{definitionId.Value:D}", activity.Tags);
    }

    [Fact]
    public async Task Uncategorized_agent_scope_does_not_query_simple_chat_history()
    {
        var projectId = Guid.NewGuid();
        var reportStore = new RecordingLlmChatProjectStructureReportStore(
            new LlmChatProjectStructureReport([], 0, 20, 0, 0m, 0, 0L, []));
        var service = new ProjectManagerSummaryQueryService(
            planAnalytics: null!,
            agentWorkspace: null!,
            simpleChatProjectStructureReportStore: reportStore,
            workflowProjectStructureReportStore: null!,
            processRunRecordStore: null!,
            processDefinitionCatalog: null!,
            clock: null!);
        var summary = CreateSummary(
            projectId,
            ProjectManagerSummaryScope.UncategorizedAgentActivity);

        var page = await service.QueryActivityPageAsync(
            new ProjectManagerActivityPageRequest(
                summary,
                ProjectManagerActivityKind.SimpleChat,
                ProjectManagerActivityStatusFilter.All));

        Assert.Empty(reportStore.Queries);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    private static ProjectManagerSummarySnapshot CreateSummary(
        Guid projectId,
        ProjectManagerSummaryScope scope = ProjectManagerSummaryScope.CurrentProject)
    {
        var options = new ProjectManagerSummaryOptions(Scope: scope);
        var resolution = new ProjectManagerSummaryScopeResolution(
            projectId,
            "Project",
            scope,
            scope == ProjectManagerSummaryScope.UncategorizedAgentActivity ? [] : [projectId],
            DescendantCount: 0,
            RequiresConfirmation: false);
        return new ProjectManagerSummarySnapshot(
            projectId,
            "Project",
            options,
            resolution,
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
}

internal sealed class RecordingLlmChatProjectStructureReportStore(
    LlmChatProjectStructureReport result) : ILlmChatProjectStructureReportStore
{
    public List<LlmChatProjectStructureReportQuery> Queries { get; } = [];

    public Task<LlmChatProjectStructureReport> QueryProjectStructureReportAsync(
        LlmChatProjectStructureReportQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Queries.Add(query);
        return Task.FromResult(result);
    }
}

internal sealed class EmptyLlmChatProjectStructureReportStore : ILlmChatProjectStructureReportStore
{
    public Task<LlmChatProjectStructureReport> QueryProjectStructureReportAsync(
        LlmChatProjectStructureReportQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new LlmChatProjectStructureReport(
            [],
            query.PageIndex,
            query.PageSize,
            0,
            0m,
            0,
            0L,
            []));
    }
}
