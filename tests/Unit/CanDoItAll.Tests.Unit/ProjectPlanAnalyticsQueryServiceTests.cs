using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectPlanAnalyticsQueryServiceTests
{
    [Fact]
    public async Task PreflightAsync_counts_only_materialized_plan_rows_and_returns_warnings()
    {
        var fixture = await AnalyticsFixture.CreateAsync(
            new ProjectPlanAnalyticsLimits(
                ConfirmationNodeCount: 3,
                ConfirmationLinkCount: 2,
                MaximumProjectCount: 5,
                MaximumNodeCount: 5,
                MaximumLinkCount: 5));
        await fixture.AddNodesAsync(
            TaskNode("task-a"),
            TaskNode("task-b"),
            WorkflowNode("workflow-a", "task-a"),
            new ProjectObjectRecord
            {
                NodeKey = "work-item:milestone",
                ObjectType = ProjectObjectType.WorkItem,
                ObjectSubtype = "milestone"
            },
            WorkflowNode("workflow-without-task", "missing-task"),
            TaskNode("system-task", isSystemManaged: true));
        await fixture.AddLinksAsync(
            Link("task-a", "task-b", ProjectObjectLinkKind.DependsOn),
            Link(
                "task-a",
                $"{ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix}daily",
                ProjectObjectLinkKind.Uses),
            Link("task-a", "missing-task", ProjectObjectLinkKind.Blocks),
            Link(
                "task-a",
                "task-b",
                ProjectObjectLinkKind.Blocks,
                isSystemManaged: true));

        var preflight = await fixture.Sut.PreflightAsync([fixture.ProjectId]);

        Assert.Equal(1, preflight.ProjectCount);
        Assert.Equal(3, preflight.PlanNodeCount);
        Assert.Equal(2, preflight.PlanLinkCount);
        Assert.Equal(5, preflight.PayloadItemCount);
        Assert.True(preflight.RequiresConfirmation);
        Assert.Equal(2, preflight.Warnings.Count);
    }

    [Fact]
    public async Task GetSummariesAsync_rejects_oversized_node_payload_before_materialization()
    {
        var fixture = await AnalyticsFixture.CreateAsync(
            new ProjectPlanAnalyticsLimits(
                ConfirmationNodeCount: 1,
                ConfirmationLinkCount: 1,
                MaximumProjectCount: 5,
                MaximumNodeCount: 1,
                MaximumLinkCount: 5));
        await fixture.AddNodesAsync(
            TaskNode("task-a"),
            TaskNode("task-b"));

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => fixture.Sut.GetSummariesAsync([fixture.ProjectId]));

        Assert.Equal(413, exception.StatusCode);
        Assert.Equal(
            ProjectPlanAnalyticsErrorCodes.PayloadLimitExceeded,
            exception.ErrorCode);
        var details = Assert.IsType<ProjectPlanAnalyticsLimitDetails>(exception.Details);
        Assert.Equal(2, details.PlanNodeCount);
        Assert.Null(details.PlanLinkCount);
    }

    [Fact]
    public async Task GetSummariesAsync_rejects_oversized_link_payload_before_materialization()
    {
        var fixture = await AnalyticsFixture.CreateAsync(
            new ProjectPlanAnalyticsLimits(
                ConfirmationNodeCount: 1,
                ConfirmationLinkCount: 1,
                MaximumProjectCount: 5,
                MaximumNodeCount: 5,
                MaximumLinkCount: 1));
        await fixture.AddNodesAsync(
            TaskNode("task-a"),
            TaskNode("task-b"));
        await fixture.AddLinksAsync(
            Link("task-a", "task-b", ProjectObjectLinkKind.DependsOn),
            Link("task-a", "task-b", ProjectObjectLinkKind.Blocks));

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => fixture.Sut.GetSummariesAsync([fixture.ProjectId]));

        Assert.Equal(
            ProjectPlanAnalyticsErrorCodes.PayloadLimitExceeded,
            exception.ErrorCode);
        var details = Assert.IsType<ProjectPlanAnalyticsLimitDetails>(exception.Details);
        Assert.Equal(2, details.PlanNodeCount);
        Assert.Equal(2, details.PlanLinkCount);
    }

    [Fact]
    public async Task GetSummariesAsync_supports_aggregate_only_query()
    {
        var fixture = await AnalyticsFixture.CreateAsync();
        await fixture.AddNodesAsync(new ProjectObjectRecord
        {
            NodeKey = "running-task",
            ObjectType = ProjectObjectType.WorkItem,
            ObjectSubtype = ProjectObjectSubtypePolicy.Task,
            Status = "running",
            ProgressPercent = 25
        });

        var summary = Assert.Single(
            await fixture.Sut.GetSummariesAsync(
                [fixture.ProjectId],
                new ProjectPlanSummaryQuery(TaskPreviewLimit: 0)));

        Assert.Equal(1, summary.TotalTaskCount);
        Assert.Empty(summary.RunningTasks);
        Assert.Empty(summary.BlockedTasks);
        Assert.Empty(summary.WaitingTasks);
    }

    [Fact]
    public async Task GetManagerSummariesAsync_schedule_only_skips_dependencies_workflows_and_assignments()
    {
        var fixture = await AnalyticsFixture.CreateAsync(
            new ProjectPlanAnalyticsLimits(
                ConfirmationNodeCount: 1,
                ConfirmationLinkCount: 1,
                MaximumProjectCount: 5,
                MaximumNodeCount: 2,
                MaximumLinkCount: 1),
            new FailingProjectPartyIntegrationBridge());
        var firstTask = TaskNode("task-a");
        firstTask.StartUtc = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        firstTask.EndUtc = firstTask.StartUtc.Value.AddHours(2);
        var secondTask = TaskNode("task-b");
        secondTask.StartUtc = firstTask.StartUtc.Value.AddHours(3);
        secondTask.EndUtc = firstTask.StartUtc.Value.AddHours(5);
        await fixture.AddNodesAsync(
            firstTask,
            secondTask,
            WorkflowNode("workflow-a", "task-a"));
        await fixture.AddLinksAsync(
            Link("task-a", "task-b", ProjectObjectLinkKind.DependsOn),
            Link("task-b", "task-a", ProjectObjectLinkKind.Blocks));

        var summary = Assert.Single(
            await fixture.Sut.GetManagerSummariesAsync(
                [fixture.ProjectId],
                new ProjectPlanManagerSummaryQuery(
                    ProjectPlanManagerSummaryMode.ScheduleOnly,
                    firstTask.StartUtc)));

        Assert.Equal(2, summary.TotalTaskCount);
        Assert.Equal(firstTask.StartUtc, summary.Schedule.EarliestStartUtc);
        Assert.Equal(secondTask.EndUtc, summary.Schedule.LatestEndUtc);
        Assert.Equal(4m, summary.Schedule.ScheduledTaskDurationHours);
        Assert.Empty(summary.FutureExpectedCostTotals);
    }

    [Fact]
    public async Task GetManagerSummariesAsync_forecast_ignores_dependency_graph_and_loads_only_cost_resource_bindings()
    {
        var fixture = await AnalyticsFixture.CreateAsync(
            new ProjectPlanAnalyticsLimits(
                ConfirmationNodeCount: 1,
                ConfirmationLinkCount: 1,
                MaximumProjectCount: 5,
                MaximumNodeCount: 3,
                MaximumLinkCount: 1));
        var task = TaskNode("task-a");
        task.StartUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        task.EndUtc = task.StartUtc.Value.AddHours(4);
        task.ProgressPercent = 50;
        task.MetadataJson = CreateCostMetadata(100m, "USD");
        await fixture.AddNodesAsync(
            task,
            TaskNode("task-b"),
            WorkflowNode("workflow-a", "task-a"));
        await fixture.AddLinksAsync(
            Link("task-a", "task-b", ProjectObjectLinkKind.DependsOn),
            Link("task-b", "task-a", ProjectObjectLinkKind.Blocks),
            Link(
                "task-a",
                $"{ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix}daily",
                ProjectObjectLinkKind.Uses));

        var preflight = await fixture.Sut.PreflightManagerSummaryAsync(
            [fixture.ProjectId],
            ProjectPlanManagerSummaryMode.ScheduleAndRemainingCosts);
        var summary = Assert.Single(
            await fixture.Sut.GetManagerSummariesAsync(
                [fixture.ProjectId],
                new ProjectPlanManagerSummaryQuery(
                    ProjectPlanManagerSummaryMode.ScheduleAndRemainingCosts,
                    task.StartUtc)));

        Assert.Equal(3, preflight.PlanNodeCount);
        Assert.Equal(1, preflight.PlanLinkCount);
        var cost = Assert.Single(summary.FutureExpectedCostTotals);
        Assert.Equal(ProjectPlanResourceGroup.Mixed, cost.Group);
        Assert.Equal(50m, cost.Amount);
        Assert.Equal("USD", cost.CurrencyCode);
    }

    [Fact]
    public async Task PreflightManagerSummaryAsync_schedule_only_counts_tasks_without_graph_rows()
    {
        var fixture = await AnalyticsFixture.CreateAsync();
        await fixture.AddNodesAsync(
            TaskNode("task-a"),
            TaskNode("task-b"),
            WorkflowNode("workflow-a", "task-a"));
        await fixture.AddLinksAsync(
            Link("task-a", "task-b", ProjectObjectLinkKind.DependsOn),
            Link(
                "task-a",
                $"{ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix}daily",
                ProjectObjectLinkKind.Uses));

        var preflight = await fixture.Sut.PreflightManagerSummaryAsync(
            [fixture.ProjectId],
            ProjectPlanManagerSummaryMode.ScheduleOnly);

        Assert.Equal(2, preflight.PlanNodeCount);
        Assert.Equal(0, preflight.PlanLinkCount);
    }

    private static string CreateCostMetadata(decimal amount, string currencyCode)
    {
        return ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExpectedCostAmount = amount,
                ExpectedCostCurrencyCode = currencyCode
            }
        });
    }

    private static ProjectObjectRecord TaskNode(
        string nodeKey,
        bool isSystemManaged = false)
    {
        return new ProjectObjectRecord
        {
            NodeKey = nodeKey,
            ObjectType = ProjectObjectType.WorkItem,
            ObjectSubtype = ProjectObjectSubtypePolicy.Task,
            IsSystemManaged = isSystemManaged
        };
    }

    private static ProjectObjectRecord WorkflowNode(
        string nodeKey,
        string parentNodeKey)
    {
        return new ProjectObjectRecord
        {
            NodeKey = nodeKey,
            ParentNodeKey = parentNodeKey,
            ObjectType = ProjectObjectType.WorkflowDefinition
        };
    }

    private static ProjectObjectLinkRecord Link(
        string sourceNodeKey,
        string targetNodeKey,
        ProjectObjectLinkKind kind,
        bool isSystemManaged = false)
    {
        return new ProjectObjectLinkRecord
        {
            SourceNodeKey = sourceNodeKey,
            TargetNodeKey = targetNodeKey,
            LinkKind = kind,
            IsSystemManaged = isSystemManaged
        };
    }

    private sealed class AnalyticsFixture(
        DbContextOptions<AppDbContext> options,
        Guid projectId,
        ProjectPlanAnalyticsQueryService sut)
    {
        public Guid ProjectId { get; } = projectId;

        public ProjectPlanAnalyticsQueryService Sut { get; } = sut;

        public static async Task<AnalyticsFixture> CreateAsync(
            ProjectPlanAnalyticsLimits? limits = null,
            IProjectPartyIntegrationBridge? partyIntegrationBridge = null)
        {
            AppDbContextModelRegistry.ConfigureAssemblies(
            [
                typeof(ProjectsModuleAssemblyMarker).Assembly,
                typeof(WorkbenchModuleAssemblyMarker).Assembly
            ]);
            var options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"plan-analytics-{Guid.NewGuid():N}")
                .Options;
            var projectId = Guid.NewGuid();
            await using var dbContext = new AppDbContext(options);
            dbContext.Set<Project>().Add(new Project
            {
                Id = projectId,
                Name = "Plan analytics",
                Slug = $"plan-analytics-{projectId:N}"
            });
            await dbContext.SaveChangesAsync();
            var factory = new TestDbContextFactory(options);
            return new AnalyticsFixture(
                options,
                projectId,
                new ProjectPlanAnalyticsQueryService(
                    factory,
                    partyIntegrationBridge ?? new NoopProjectPartyIntegrationBridge(),
                    new ProjectPlanSummaryCalculator(),
                    limits ?? ProjectPlanAnalyticsLimits.Default));
        }

        public async Task AddNodesAsync(params ProjectObjectRecord[] nodes)
        {
            await using var dbContext = new AppDbContext(options);
            foreach (var node in nodes)
            {
                node.ProjectId = ProjectId;
                node.Title = node.NodeKey;
            }

            dbContext.Set<ProjectObjectRecord>().AddRange(nodes);
            await dbContext.SaveChangesAsync();
        }

        public async Task AddLinksAsync(params ProjectObjectLinkRecord[] links)
        {
            await using var dbContext = new AppDbContext(options);
            foreach (var link in links)
            {
                link.ProjectId = ProjectId;
            }

            dbContext.Set<ProjectObjectLinkRecord>().AddRange(links);
            await dbContext.SaveChangesAsync();
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

    private sealed class FailingProjectPartyIntegrationBridge : IProjectPartyIntegrationBridge
    {
        private static InvalidOperationException UnexpectedCall()
        {
            return new InvalidOperationException(
                "The schedule-only manager summary must not load project-party data.");
        }

        public Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<ProjectPartyOption?> GetPartyOptionAsync(
            Guid partyId,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<Result<Guid>> SaveAssignmentAsync(
            ProjectPartyAssignmentUpsertRequest request,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<Result> ReplaceNodeAssignmentsAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task DeleteAssignmentAsync(
            Guid assignmentId,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task DeleteAssignmentsForNodesAsync(
            Guid projectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task MoveAssignmentsToProjectAsync(
            Guid sourceProjectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            Guid targetProjectId,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();

        public Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
            ProjectPartyQuickCreateRequest request,
            CancellationToken cancellationToken = default)
            => throw UnexpectedCall();
    }
}
