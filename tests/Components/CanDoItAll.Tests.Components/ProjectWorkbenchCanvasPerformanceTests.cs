using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectWorkbenchCanvasPerformanceTests
{
    [Fact]
    public async Task Caller_controlled_create_skips_projection_assembly_for_root_and_canonical_parents()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(UseProjectionProbe);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectionProbe = harness.Context.Services.GetRequiredService<ProjectionContributorProbe>();
        var projectId = await CreateProjectAsync(projectsService, "Targeted create planning");

        projectionProbe.Reset();

        var parent = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Fast root child",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                240,
                180));
        var child = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Fast canonical child",
                string.Empty,
                string.Empty,
                parent.Id,
                520,
                180));

        Assert.Equal(0, projectionProbe.InvocationCount);
        Assert.Equal(parent.Id, child.ParentId);

        await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Automatic child",
                string.Empty,
                string.Empty,
                parent.Id,
                760,
                180,
                PlacementIntent: ProjectObjectPlacementIntent.AutomaticAroundParent));

        Assert.Equal(1, projectionProbe.InvocationCount);
    }

    [Fact]
    public async Task Canonical_move_skips_projection_assembly_while_mixed_move_preserves_projection_layouts()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(UseProjectionProbe);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectionProbe = harness.Context.Services.GetRequiredService<ProjectionContributorProbe>();
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projectsService, "Targeted move persistence");
        var canonical = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Canonical move target",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                220,
                180));

        projectionProbe.Reset();

        var canonicalMove = await workbenchService.MoveObjectsAsync(
            projectId,
            [new ProjectNodeMoveRequest(canonical.Id, 540, 320)]);

        Assert.Equal(0, projectionProbe.InvocationCount);
        Assert.Equal(canonical.Id, Assert.Single(canonicalMove));

        projectionProbe.Reset();

        var mixedMove = await workbenchService.MoveObjectsAsync(
            projectId,
            [
                new ProjectNodeMoveRequest(canonical.Id, 620, 360),
                new ProjectNodeMoveRequest(ProjectionContributorProbe.ProjectionNodeKey, 880, 420)
            ]);

        Assert.Equal(1, projectionProbe.InvocationCount);
        Assert.Contains(canonical.Id, mixedMove);
        Assert.Contains(ProjectionContributorProbe.ProjectionNodeKey, mixedMove);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var projectionLayout = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .SingleAsync(layout =>
                layout.ProjectId == projectId &&
                layout.NodeKey == ProjectionContributorProbe.ProjectionNodeKey);

        Assert.Equal(880d, projectionLayout.PositionX);
        Assert.Equal(420d, projectionLayout.PositionY);
    }

    private static void UseProjectionProbe(IServiceCollection services)
    {
        services.RemoveAll<IProjectStructureProjectionContributor>();
        services.AddSingleton<ProjectionContributorProbe>();
        services.AddSingleton<IProjectStructureProjectionContributor>(
            serviceProvider => serviceProvider.GetRequiredService<ProjectionContributorProbe>());
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var project = await projectsService.GetAsync(null);
        project.Name = name;
        project.Description = $"{name} description";
        project.Objective = $"{name} objective";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        return saveResult.Value;
    }

    private sealed class ProjectionContributorProbe : IProjectStructureProjectionContributor
    {
        public const string ProjectionNodeKey = "projection:performance-probe";

        private int invocationCount;

        public int InvocationCount => invocationCount;

        public Task ContributeAsync(
            ProjectStructureProjectionContext context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref invocationCount);
            context.AddNode(new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = ProjectionNodeKey,
                ObjectType = ProjectObjectType.Phase,
                Title = "Projection performance probe",
                PositionX = 720,
                PositionY = 260,
                CreatedAtUtc = context.AssembledAtUtc,
                UpdatedAtUtc = context.AssembledAtUtc
            });
            return Task.CompletedTask;
        }

        public void Reset()
        {
            Interlocked.Exchange(ref invocationCount, 0);
        }
    }
}
