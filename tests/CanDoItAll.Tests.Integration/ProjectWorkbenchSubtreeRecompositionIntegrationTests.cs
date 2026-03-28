using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectWorkbenchSubtreeRecompositionIntegrationTests
{
    [Fact]
    public async Task RecomposeSubtreeAsync_preserves_links_and_keeps_the_recomposed_branch_collision_free()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projects, "Subtree recomposition");
        var branchRoot = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Branch root",
                string.Empty,
                "Recompose descendants around this node.",
                BuildProjectRootNodeKey(projectId),
                840,
                360));
        var obstacle = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Obstacle",
                string.Empty,
                "Sibling node that must stay fixed.",
                BuildProjectRootNodeKey(projectId),
                540,
                360));
        var capture = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Capture",
                string.Empty,
                "First branch segment.",
                branchRoot.Id,
                1120,
                220));
        var review = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Review",
                string.Empty,
                "Second branch segment.",
                branchRoot.Id,
                1180,
                340));
        var delivery = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Delivery",
                string.Empty,
                "Third branch segment.",
                branchRoot.Id,
                1220,
                460));
        var captureTask = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Capture task",
                string.Empty,
                "Nested task.",
                capture.Id,
                1440,
                220,
                null,
                null,
                "task"));
        var reviewTask = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Review task",
                string.Empty,
                "Nested task.",
                review.Id,
                1480,
                340,
                null,
                null,
                "task"));
        var deliveryTask = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Delivery task",
                string.Empty,
                "Nested task.",
                delivery.Id,
                1520,
                460,
                null,
                null,
                "task"));
        await workbench.LinkObjectsAsync(projectId, obstacle.Id, reviewTask.Id, ProjectObjectLinkKind.DependsOn);

        var surfaceBefore = await workbench.GetStructureAsync(projectId);
        var linkSetBefore = surfaceBefore.Links
            .Select(link => $"{link.SourceId}|{link.TargetId}|{link.Kind}|{link.IsUserAuthored}")
            .OrderBy(link => link, StringComparer.Ordinal)
            .ToList();

        var result = await workbench.RecomposeSubtreeAsync(projectId, branchRoot.Id);

        Assert.NotNull(result);
        Assert.Equal(6, result!.DescendantCount);
        Assert.True(result.RepositionedNodeCount > 0);

        var surfaceAfter = await workbench.GetStructureAsync(projectId);
        var linkSetAfter = surfaceAfter.Links
            .Select(link => $"{link.SourceId}|{link.TargetId}|{link.Kind}|{link.IsUserAuthored}")
            .OrderBy(link => link, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(linkSetBefore, linkSetAfter);

        var reloadedRoot = Assert.Single(surfaceAfter.Nodes, node => node.Id == branchRoot.Id);
        var reloadedObstacle = Assert.Single(surfaceAfter.Nodes, node => node.Id == obstacle.Id);
        Assert.Equal(branchRoot.X, reloadedRoot.X);
        Assert.Equal(branchRoot.Y, reloadedRoot.Y);
        Assert.Equal(obstacle.X, reloadedObstacle.X);
        Assert.Equal(obstacle.Y, reloadedObstacle.Y);

        var descendantIds = new HashSet<string>(
            [capture.Id, review.Id, delivery.Id, captureTask.Id, reviewTask.Id, deliveryTask.Id],
            StringComparer.Ordinal);
        var descendants = surfaceAfter.Nodes
            .Where(node => descendantIds.Contains(node.Id))
            .ToList();

        Assert.Contains(descendants, node => node.X < reloadedRoot.X);
        Assert.Contains(descendants, node => node.X > reloadedRoot.X);
        Assert.Contains(descendants, node => node.Y < reloadedRoot.Y);
        Assert.Contains(descendants, node => node.Y > reloadedRoot.Y);
        AssertNoOverlaps([reloadedRoot, reloadedObstacle, .. descendants]);
    }

    private static void AssertNoOverlaps(IReadOnlyList<ProjectStructureNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            for (var j = i + 1; j < nodes.Count; j++)
            {
                Assert.False(
                    Overlaps(nodes[i], nodes[j]),
                    $"{nodes[i].Title} overlaps {nodes[j].Title} after subtree recomposition.");
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
