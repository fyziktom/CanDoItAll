using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectWorkbenchSubtreeRecompositionIntegrationTests
{
    private const double FullTurn = Math.PI * 2d;
    private const double TopClockAngle = 0d;
    private const double BranchBubblePadding = 24d;

    [Fact]
    public async Task RecomposeSubtreeAsync_places_first_layer_in_clockface_slots_and_keeps_branch_groups_separated()
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
                520,
                360));
        var discovery = await CreateBranchAsync(workbench, projectId, branchRoot.Id, "Discovery", 1120, 180);
        var build = await CreateBranchAsync(workbench, projectId, branchRoot.Id, "Build", 1160, 260);
        var validate = await CreateBranchAsync(workbench, projectId, branchRoot.Id, "Validate", 1200, 340);
        var release = await CreateBranchAsync(workbench, projectId, branchRoot.Id, "Release", 1240, 420);
        await workbench.LinkObjectsAsync(projectId, obstacle.Id, validate.LeafIds[1], ProjectObjectLinkKind.DependsOn);

        var surfaceBefore = await workbench.GetStructureAsync(projectId);
        var linkSetBefore = surfaceBefore.Links
            .Select(link => $"{link.SourceId}|{link.TargetId}|{link.Kind}|{link.IsUserAuthored}")
            .OrderBy(link => link, StringComparer.Ordinal)
            .ToList();

        var result = await workbench.RecomposeSubtreeAsync(projectId, branchRoot.Id);

        Assert.NotNull(result);
        Assert.Equal(12, result!.DescendantCount);
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

        var nodesById = surfaceAfter.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        AssertClockfaceSlot(reloadedRoot, nodesById[discovery.RootId], 0, 4);
        AssertClockfaceSlot(reloadedRoot, nodesById[build.RootId], 1, 4);
        AssertClockfaceSlot(reloadedRoot, nodesById[validate.RootId], 2, 4);
        AssertClockfaceSlot(reloadedRoot, nodesById[release.RootId], 3, 4);

        var branches = new[]
        {
            discovery,
            build,
            validate,
            release
        };

        AssertBranchDescendantsStayClosestToTheirOwnBranch(reloadedRoot, nodesById, branches);
        AssertBranchBubblesDoNotOverlap(nodesById, branches);
        AssertNoOverlaps(
            [
                reloadedRoot,
                reloadedObstacle,
                .. branches.SelectMany(branch => branch.AllNodeIds).Select(nodeId => nodesById[nodeId])
            ]);
    }

    [Fact]
    public async Task RecomposeSubtreeAsync_with_single_child_branch_keeps_descendants_compact_and_below_root()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projects, "Single-branch subtree recomposition");
        var projectRootId = BuildProjectRootNodeKey(projectId);
        var plan = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Backfilled execution plan",
                "JsonOutline import",
                "Compact single-branch plan.",
                projectRootId,
                860,
                360,
                null,
                null,
                "delivery"));
        var acceptanceScope = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Acceptance scope",
                string.Empty,
                "Validation scope.",
                plan.Id,
                1160,
                240,
                null,
                null,
                "delivery"));
        var acceptanceTask = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Document the regression scope",
                string.Empty,
                "Acceptance scope task.",
                acceptanceScope.Id,
                1380,
                220,
                null,
                null,
                "task"));
        var validationProof = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Validation proof",
                string.Empty,
                "Collect build and browser proof.",
                plan.Id,
                1180,
                320,
                null,
                null,
                "delivery"));
        var validationTask = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Run build and browser checks",
                string.Empty,
                "Validation task.",
                validationProof.Id,
                1410,
                300,
                null,
                null,
                "task"));
        var objective = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Objective",
                string.Empty,
                "Root objective.",
                plan.Id,
                1200,
                400,
                null,
                null,
                "task"));
        var prerequisites = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Prerequisites",
                string.Empty,
                "Reopen dependencies if proof is weak.",
                plan.Id,
                1220,
                480,
                null,
                null,
                "delivery"));
        var prerequisitesTask = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Verify bundle dependencies",
                string.Empty,
                "Prerequisite task.",
                prerequisites.Id,
                1450,
                500,
                null,
                null,
                "task"));
        var dependencyImpact = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Dependency impact",
                string.Empty,
                "Review downstream risk.",
                plan.Id,
                1240,
                560,
                null,
                null,
                "delivery"));

        var surfaceBefore = await workbench.GetStructureAsync(projectId);
        var root = Assert.Single(surfaceBefore.Nodes, node => node.Id == projectRootId);

        var createdNodeIds = new[]
        {
            plan.Id,
            acceptanceScope.Id,
            acceptanceTask.Id,
            validationProof.Id,
            validationTask.Id,
            objective.Id,
            prerequisites.Id,
            prerequisitesTask.Id,
            dependencyImpact.Id
        };

        var result = await workbench.RecomposeSubtreeAsync(projectId, projectRootId);

        Assert.NotNull(result);
        Assert.Equal(createdNodeIds.Length, result!.DescendantCount);
        Assert.True(result.RepositionedNodeCount > 0);

        var surfaceAfter = await workbench.GetStructureAsync(projectId);
        var nodesById = surfaceAfter.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var reloadedRoot = nodesById[projectRootId];
        var reloadedPlan = nodesById[plan.Id];
        var descendants = createdNodeIds
            .Select(nodeId => nodesById[nodeId])
            .ToList();
        var descendantBounds = ResolveBounds(descendants);
        var descendantWidth = descendantBounds.Right - descendantBounds.Left;
        var descendantHeight = descendantBounds.Bottom - descendantBounds.Top;

        Assert.Equal(root.X, reloadedRoot.X);
        Assert.Equal(root.Y, reloadedRoot.Y);
        Assert.True(reloadedPlan.Y > reloadedRoot.Y + 120d, "The single branch root should move below the selected root.");
        Assert.True(reloadedPlan.Y < reloadedRoot.Y + 1200d, "The single branch root should stay within a compact distance from the selected root.");
        Assert.True(descendantWidth < 2600d, $"Single-branch recomposition should stay compact, but width was {descendantWidth:F1}.");
        Assert.True(descendantHeight < 2600d, $"Single-branch recomposition should stay compact, but height was {descendantHeight:F1}.");

        foreach (var descendant in descendants)
        {
            Assert.True(
                descendant.Y > reloadedRoot.Y + 40d,
                $"{descendant.Title} should stay below the selected root in a single-branch recomposition.");
        }

        AssertNoOverlaps([reloadedRoot, .. descendants]);
    }

    private static async Task<BranchFixture> CreateBranchAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string parentId,
        string title,
        double x,
        double y)
    {
        var branch = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                title,
                string.Empty,
                $"{title} branch.",
                parentId,
                x,
                y));
        var task = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                $"{title} task",
                string.Empty,
                "Branch task.",
                branch.Id,
                x + 220,
                y - 20,
                null,
                null,
                "task"));
        var check = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                $"{title} check",
                string.Empty,
                "Branch check.",
                branch.Id,
                x + 260,
                y + 40,
                null,
                null,
                "task"));

        return new BranchFixture(branch.Id, [branch.Id, task.Id, check.Id], [task.Id, check.Id]);
    }

    private static void AssertClockfaceSlot(
        ProjectStructureNode root,
        ProjectStructureNode node,
        int slotIndex,
        int slotCount)
    {
        var expectedAngle = TopClockAngle + ((FullTurn / slotCount) * slotIndex);
        var actualAngle = ResolveClockfaceAngle(root, node);
        Assert.True(
            CircularDistance(actualAngle, expectedAngle) <= 0.42d,
            $"{node.Title} should be near slot {slotIndex}, but was at {actualAngle:F2} radians.");
    }

    private static void AssertBranchDescendantsStayClosestToTheirOwnBranch(
        ProjectStructureNode root,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlyList<BranchFixture> branches)
    {
        var branchCenterAngles = branches.ToDictionary(
            branch => branch.RootId,
            branch => ResolveClockfaceAngle(root, nodesById[branch.RootId]),
            StringComparer.Ordinal);

        foreach (var branch in branches)
        {
            foreach (var leafId in branch.LeafIds)
            {
                var node = nodesById[leafId];
                var nodeAngle = ResolveClockfaceAngle(root, node);
                var ownDelta = CircularDistance(nodeAngle, branchCenterAngles[branch.RootId]);
                var nearestSiblingDelta = branches
                    .Where(candidate => !string.Equals(candidate.RootId, branch.RootId, StringComparison.Ordinal))
                    .Select(candidate => CircularDistance(nodeAngle, branchCenterAngles[candidate.RootId]))
                    .Min();

                Assert.True(
                    ownDelta + 0.05d < nearestSiblingDelta,
                    $"{node.Title} drifted closer to a sibling branch than its own branch.");
            }
        }
    }

    private static void AssertBranchBubblesDoNotOverlap(
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlyList<BranchFixture> branches)
    {
        for (var i = 0; i < branches.Count; i++)
        {
            var leftBounds = InflateBounds(ResolveBounds(branches[i].AllNodeIds.Select(nodeId => nodesById[nodeId])), BranchBubblePadding);

            for (var j = i + 1; j < branches.Count; j++)
            {
                var rightBounds = InflateBounds(ResolveBounds(branches[j].AllNodeIds.Select(nodeId => nodesById[nodeId])), BranchBubblePadding);
                Assert.False(
                    BoundsOverlap(leftBounds, rightBounds),
                    $"Branch bubble {branches[i].RootId} overlaps branch bubble {branches[j].RootId}.");
            }
        }
    }

    private static void AssertNoOverlaps(IReadOnlyList<ProjectStructureNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            for (var j = i + 1; j < nodes.Count; j++)
            {
                Assert.False(
                    BoundsOverlap(ResolveBounds(nodes[i]), ResolveBounds(nodes[j])),
                    $"{nodes[i].Title} overlaps {nodes[j].Title} after subtree recomposition.");
            }
        }
    }

    private static double ResolveClockfaceAngle(ProjectStructureNode origin, ProjectStructureNode node)
        => NormalizeAngle(Math.Atan2(node.Y - origin.Y, node.X - origin.X) + (Math.PI / 2d));

    private static double CircularDistance(double left, double right)
    {
        var delta = Math.Abs(left - right);
        return Math.Min(delta, FullTurn - delta);
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

    private static (double Left, double Top, double Right, double Bottom) ResolveBounds(IEnumerable<ProjectStructureNode> nodes)
    {
        var bounds = nodes.Select(ResolveBounds).ToList();
        return (
            bounds.Min(item => item.Left),
            bounds.Min(item => item.Top),
            bounds.Max(item => item.Right),
            bounds.Max(item => item.Bottom));
    }

    private static (double Left, double Top, double Right, double Bottom) InflateBounds(
        (double Left, double Top, double Right, double Bottom) bounds,
        double padding)
        => (
            bounds.Left - padding,
            bounds.Top - padding,
            bounds.Right + padding,
            bounds.Bottom + padding);

    private static bool BoundsOverlap(
        (double Left, double Top, double Right, double Bottom) left,
        (double Left, double Top, double Right, double Bottom) right)
        => left.Left < right.Right &&
           left.Right > right.Left &&
           left.Top < right.Bottom &&
           left.Bottom > right.Top;

    private static double NormalizeAngle(double angle)
    {
        while (angle < 0d)
        {
            angle += FullTurn;
        }

        while (angle >= FullTurn)
        {
            angle -= FullTurn;
        }

        return angle;
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

    private sealed record BranchFixture(string RootId, IReadOnlyList<string> AllNodeIds, IReadOnlyList<string> LeafIds);
}
