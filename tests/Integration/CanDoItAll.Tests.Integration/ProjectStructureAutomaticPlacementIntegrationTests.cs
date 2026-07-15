using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureAutomaticPlacementIntegrationTests
{
    private static readonly ProjectStructureAgentContext Agent = new(
        "automatic-placement-agent",
        "Automatic Placement Agent",
        "integration-machine",
        IntegrationTestPaths.RepositoryRoot,
        "tests/automatic-placement",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Static_test_plan_projection_precedes_process_child_composition()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var contributorTypes = scope.ServiceProvider
            .GetServices<IProjectStructureProjectionContributor>()
            .Select(contributor => contributor.GetType())
            .ToList();

        var testPlanIndex = contributorTypes.IndexOf(typeof(TestPlanProjectionContributor));
        var processIndex = contributorTypes.IndexOf(typeof(ProjectStructureProcessProjectionContributor));

        Assert.True(testPlanIndex >= 0);
        Assert.True(processIndex > testPlanIndex);
    }

    [Fact]
    public async Task Agent_create_treats_supplied_coordinates_as_a_hint_while_caller_coordinates_remain_exact()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects);
        var parent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Architecture",
                "Parent block",
                string.Empty,
                $"project:{projectId:D}",
                600,
                400,
                ObjectSubtype: "architecture"));
        var occupiedSibling = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Existing child",
                "Occupied placement",
                string.Empty,
                parent.Id,
                936,
                400,
                ObjectSubtype: "architecture"));

        var created = await agentService.CreateNodeAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                "Agent child",
                "Automatically composed",
                string.Empty,
                parent.Id,
                occupiedSibling.X,
                occupiedSibling.Y,
                ObjectSubtype: "architecture"),
            Agent);

        Assert.Equal(600d, parent.X);
        Assert.Equal(400d, parent.Y);
        Assert.Equal(936d, occupiedSibling.X);
        Assert.Equal(400d, occupiedSibling.Y);
        Assert.Equal(parent.Id, created.ParentId);
        Assert.NotNull(created.X);
        Assert.NotNull(created.Y);
        Assert.True(created.X!.Value > parent.X);
        Assert.NotEqual(
            (occupiedSibling.X, occupiedSibling.Y),
            (created.X.Value, created.Y!.Value));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Automatic placement integration",
            Description = "Automatic placement integration description"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
