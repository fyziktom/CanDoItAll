using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectManagerSummaryScopeResolverTests
{
    [Fact]
    public async Task ResolveAsync_reads_only_requested_descendant_closure_and_tolerates_cycles()
    {
        var fixture = await ScopeFixture.CreateAsync();
        var childId = Guid.NewGuid();
        var grandchildId = Guid.NewGuid();
        var unrelatedParentId = Guid.NewGuid();
        var unrelatedChildId = Guid.NewGuid();
        await fixture.AddProjectsAsync(
            childId,
            grandchildId,
            unrelatedParentId,
            unrelatedChildId);
        await fixture.AddLinksAsync(
            (fixture.RootProjectId, childId),
            (childId, grandchildId),
            (grandchildId, fixture.RootProjectId),
            (unrelatedParentId, unrelatedChildId));

        var resolution = await fixture.Sut.ResolveAsync(
            fixture.RootProjectId,
            ProjectManagerSummaryScope.ProjectAndDescendants,
            ProjectManagerSummaryContentMode.HistoryOnly);

        Assert.Equal(fixture.RootProjectId, resolution.ProjectIds[0]);
        Assert.Equal(
            new HashSet<Guid>
            {
                fixture.RootProjectId,
                childId,
                grandchildId
            },
            resolution.ProjectIds.ToHashSet());
        Assert.Equal(2, resolution.DescendantCount);
        Assert.DoesNotContain(unrelatedParentId, resolution.ProjectIds);
        Assert.DoesNotContain(unrelatedChildId, resolution.ProjectIds);
        Assert.NotNull(resolution.PlanPreflight);
    }

    [Fact]
    public async Task ResolveAsync_warns_below_absolute_limit_and_rejects_scope_above_it()
    {
        var fixture = await ScopeFixture.CreateAsync(
            new ProjectManagerSummaryScopeLimits(
                ConfirmationDescendantCount: 1,
                MaximumProjectCount: 3,
                HierarchyFrontierBatchSize: 2));
        var firstChildId = Guid.NewGuid();
        var secondChildId = Guid.NewGuid();
        var thirdChildId = Guid.NewGuid();
        await fixture.AddProjectsAsync(
            firstChildId,
            secondChildId,
            thirdChildId);
        await fixture.AddLinksAsync((fixture.RootProjectId, firstChildId));

        var warningResolution = await fixture.Sut.ResolveAsync(
            fixture.RootProjectId,
            ProjectManagerSummaryScope.ProjectAndDescendants,
            ProjectManagerSummaryContentMode.HistoryOnly);

        Assert.True(warningResolution.RequiresConfirmation);
        Assert.Equal(1, warningResolution.DescendantCount);

        await fixture.AddLinksAsync(
            (fixture.RootProjectId, secondChildId),
            (fixture.RootProjectId, thirdChildId));

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => fixture.Sut.ResolveAsync(
                fixture.RootProjectId,
                ProjectManagerSummaryScope.ProjectAndDescendants,
                ProjectManagerSummaryContentMode.HistoryOnly));

        Assert.Equal(413, exception.StatusCode);
        Assert.Equal(
            ProjectPlanAnalyticsErrorCodes.ScopeLimitExceeded,
            exception.ErrorCode);
        Assert.Contains("smaller subtree", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAsync_uses_content_specific_plan_preflight()
    {
        var fixture = await ScopeFixture.CreateAsync();
        await fixture.AddPlanNodesAsync(
            new ProjectObjectRecord
            {
                NodeKey = "task-a",
                ObjectType = ProjectObjectType.WorkItem,
                ObjectSubtype = ProjectObjectSubtypePolicy.Task
            },
            new ProjectObjectRecord
            {
                NodeKey = "task-b",
                ObjectType = ProjectObjectType.WorkItem,
                ObjectSubtype = ProjectObjectSubtypePolicy.Task
            },
            new ProjectObjectRecord
            {
                NodeKey = "workflow-a",
                ParentNodeKey = "task-a",
                ObjectType = ProjectObjectType.WorkflowDefinition
            });
        await fixture.AddPlanLinksAsync(
            new ProjectObjectLinkRecord
            {
                SourceNodeKey = "task-a",
                TargetNodeKey = "task-b",
                LinkKind = ProjectObjectLinkKind.DependsOn
            },
            new ProjectObjectLinkRecord
            {
                SourceNodeKey = "task-b",
                TargetNodeKey = "task-a",
                LinkKind = ProjectObjectLinkKind.Blocks
            },
            new ProjectObjectLinkRecord
            {
                SourceNodeKey = "task-a",
                TargetNodeKey = $"{ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix}daily",
                LinkKind = ProjectObjectLinkKind.Uses
            });

        var history = await fixture.Sut.ResolveAsync(
            fixture.RootProjectId,
            ProjectManagerSummaryScope.CurrentProject,
            ProjectManagerSummaryContentMode.HistoryOnly);
        var forecast = await fixture.Sut.ResolveAsync(
            fixture.RootProjectId,
            ProjectManagerSummaryScope.CurrentProject,
            ProjectManagerSummaryContentMode.HistoryAndFuture);

        Assert.Equal(2, history.PlanPreflight!.PlanNodeCount);
        Assert.Equal(0, history.PlanPreflight.PlanLinkCount);
        Assert.Equal(3, forecast.PlanPreflight!.PlanNodeCount);
        Assert.Equal(1, forecast.PlanPreflight.PlanLinkCount);
    }

    private sealed class ScopeFixture(
        DbContextOptions<AppDbContext> options,
        Guid rootProjectId,
        ProjectManagerSummaryScopeResolver sut)
    {
        public Guid RootProjectId { get; } = rootProjectId;

        public ProjectManagerSummaryScopeResolver Sut { get; } = sut;

        public static async Task<ScopeFixture> CreateAsync(
            ProjectManagerSummaryScopeLimits? scopeLimits = null)
        {
            AppDbContextModelRegistry.ConfigureAssemblies(
            [
                typeof(ProjectsModuleAssemblyMarker).Assembly,
                typeof(WorkbenchModuleAssemblyMarker).Assembly
            ]);
            var options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"manager-summary-scope-{Guid.NewGuid():N}")
                .Options;
            var factory = new TestDbContextFactory(options);
            var analytics = new ProjectPlanAnalyticsQueryService(
                factory,
                new NoopProjectPartyIntegrationBridge(),
                new ProjectPlanSummaryCalculator());
            var rootProjectId = Guid.NewGuid();
            await using var dbContext = new AppDbContext(options);
            dbContext.Set<Project>().Add(CreateProject(rootProjectId));
            await dbContext.SaveChangesAsync();
            return new ScopeFixture(
                options,
                rootProjectId,
                new ProjectManagerSummaryScopeResolver(
                    factory,
                    analytics,
                    scopeLimits ?? ProjectManagerSummaryScopeLimits.Default));
        }

        public async Task AddProjectsAsync(params Guid[] projectIds)
        {
            await using var dbContext = new AppDbContext(options);
            dbContext.Set<Project>().AddRange(projectIds.Select(CreateProject));
            await dbContext.SaveChangesAsync();
        }

        public async Task AddLinksAsync(
            params (Guid ParentProjectId, Guid ChildProjectId)[] links)
        {
            await using var dbContext = new AppDbContext(options);
            dbContext.Set<ProjectHierarchyLink>().AddRange(links.Select(link => new ProjectHierarchyLink
            {
                ParentProjectId = link.ParentProjectId,
                ChildProjectId = link.ChildProjectId
            }));
            await dbContext.SaveChangesAsync();
        }

        public async Task AddPlanNodesAsync(params ProjectObjectRecord[] nodes)
        {
            await using var dbContext = new AppDbContext(options);
            foreach (var node in nodes)
            {
                node.ProjectId = RootProjectId;
                node.Title = node.NodeKey;
            }

            dbContext.Set<ProjectObjectRecord>().AddRange(nodes);
            await dbContext.SaveChangesAsync();
        }

        public async Task AddPlanLinksAsync(params ProjectObjectLinkRecord[] links)
        {
            await using var dbContext = new AppDbContext(options);
            foreach (var link in links)
            {
                link.ProjectId = RootProjectId;
            }

            dbContext.Set<ProjectObjectLinkRecord>().AddRange(links);
            await dbContext.SaveChangesAsync();
        }

        private static Project CreateProject(Guid projectId)
        {
            return new Project
            {
                Id = projectId,
                Name = $"Project {projectId:N}",
                Slug = $"project-{projectId:N}"
            };
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
}
