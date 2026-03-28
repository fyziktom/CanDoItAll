using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageRecompositionTests
{
    [Fact]
    public async Task Recompose_toolbar_action_redistributes_the_selected_branch_across_the_available_space()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Recomposition Component");
        var branchRoot = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Branch root",
                string.Empty,
                "Manual recomposition anchor.",
                BuildProjectRootNodeKey(projectId),
                760,
                360));
        var descendants = await CreateRightWeightedBranchAsync(workbenchService, projectId, branchRoot.Id);
        await SaveSelectedNodeStateAsync(workbenchService, projectId, branchRoot.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find("[data-testid='project-structure-recompose-toolbar-button']");
            Assert.Equal("Recompose", button.TextContent.Trim());
            Assert.Null(button.GetAttribute("disabled"));
        });

        cut.Find("[data-testid='project-structure-recompose-toolbar-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Recomposed 4 nodes under Branch root.", cut.Markup);
        });

        var surface = await workbenchService.GetStructureAsync(projectId);
        var reloadedRoot = Assert.Single(surface.Nodes, node => node.Id == branchRoot.Id);
        Assert.Equal(branchRoot.X, reloadedRoot.X);
        Assert.Equal(branchRoot.Y, reloadedRoot.Y);

        var movedDescendants = surface.Nodes
            .Where(node => descendants.Contains(node.Id, StringComparer.Ordinal))
            .ToList();
        Assert.Equal(4, movedDescendants.Count);
        Assert.Contains(movedDescendants, node => node.X < reloadedRoot.X);
        Assert.Contains(movedDescendants, node => node.X > reloadedRoot.X);
        Assert.Contains(movedDescendants, node => node.Y < reloadedRoot.Y);
        Assert.Contains(movedDescendants, node => node.Y > reloadedRoot.Y);
        AssertNoOverlaps([reloadedRoot, .. movedDescendants]);
    }

    private static async Task<IReadOnlyList<string>> CreateRightWeightedBranchAsync(
        ProjectWorkbenchService workbenchService,
        Guid projectId,
        string parentId)
    {
        var first = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Capture",
                string.Empty,
                "First branch child.",
                parentId,
                1040,
                220));
        var second = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Review",
                string.Empty,
                "Second branch child.",
                parentId,
                1100,
                320));
        var third = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Delivery",
                string.Empty,
                "Third branch child.",
                parentId,
                1120,
                420));
        var fourth = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Follow-up",
                string.Empty,
                "Fourth branch child.",
                parentId,
                1160,
                520));

        return [first.Id, second.Id, third.Id, fourth.Id];
    }

    private static void AssertNoOverlaps(IReadOnlyList<ProjectStructureNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            for (var j = i + 1; j < nodes.Count; j++)
            {
                Assert.False(
                    Overlaps(nodes[i], nodes[j]),
                    $"{nodes[i].Title} overlaps {nodes[j].Title} after recomposition.");
            }
        }
    }

    private static bool Overlaps(ProjectStructureNode left, ProjectStructureNode right)
    {
        var leftBounds = ResolveBounds(left);
        var rightBounds = ResolveBounds(right);
        return leftBounds.Left < rightBounds.Right &&
               leftBounds.Right > rightBounds.Left &&
               leftBounds.Top < rightBounds.Bottom &&
               leftBounds.Bottom > rightBounds.Top;
    }

    private static (double Left, double Top, double Right, double Bottom) ResolveBounds(ProjectStructureNode node)
    {
        var (width, height) = node.VisualProfile.Shape switch
        {
            "circle" => (104d, 104d),
            "pill" => (196d, 64d),
            _ => (204d, 80d)
        };

        return (
            node.X - (width / 2d),
            node.Y - (height / 2d),
            node.X + (width / 2d),
            node.Y + (height / 2d));
    }

    private static Task SaveSelectedNodeStateAsync(ProjectWorkbenchService workbenchService, Guid projectId, params string[] selectedNodeIds)
        => workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = selectedNodeIds.ToList(),
                WindowStates = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal)
                {
                    ["project-structure.selection"] = new CanvasWorkbenchWindowState { IsVisible = true }
                }
            }.ToJson());

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static string BuildProjectRootNodeKey(Guid projectId)
        => $"project:{projectId}";
}
