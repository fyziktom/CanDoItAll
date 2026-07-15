using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureAutomaticPlacementPolicyTests
{
    [Fact]
    public void Resolve_InheritedRequiredRightDirection_KeepsSequentialChildrenRightAndSeparate()
    {
        var definition = CreateNode(
            "definition",
            null,
            ProjectObjectType.ProcessDefinition,
            200d,
            400d);
        var run = CreateNode(
            "run",
            definition.NodeKey,
            ProjectObjectType.ProcessRun,
            600d,
            400d);
        ProjectObjectRecord[] nodes = [definition, run];
        List<ProjectObjectRecord> children = [];
        var session = new ProjectStructureAutomaticPlacementSession(nodes);
        var direction = session.ResolveIncomingDirection(run.NodeKey);
        var childSpecifications = new[]
        {
            (NodeKey: "output", ObjectType: ProjectObjectType.File, Title: "Run artifacts"),
            (NodeKey: "summary", ObjectType: ProjectObjectType.Note, Title: "Run summary"),
            (NodeKey: "screenshot", ObjectType: ProjectObjectType.ImageAsset, Title: "Screenshot"),
            (NodeKey: "runtime", ObjectType: ProjectObjectType.Environment, Title: "Run final app")
        };

        Assert.Equal(ProjectStructurePlacementDirection.Right, direction);

        for (var index = 0; index < childSpecifications.Length; index++)
        {
            var specification = childSpecifications[index];
            var inheritedRequest = CreateRequest(run.NodeKey, specification.ObjectType, specification.Title);
            var requiredRequest = inheritedRequest with { RequiredDirection = direction };
            var firstPlacement = session.Resolve(requiredRequest);
            var repeatedPlacement = session.Resolve(requiredRequest);

            Assert.Equal(firstPlacement, repeatedPlacement);
            Assert.True(firstPlacement.X > run.PositionX);
            if (index == 0)
            {
                Assert.Equal(
                    firstPlacement,
                    session.Resolve(inheritedRequest));
            }

            var child = CreateNode(
                specification.NodeKey,
                run.NodeKey,
                specification.ObjectType,
                firstPlacement.X,
                firstPlacement.Y,
                specification.Title);
            session.Add(child);
            children.Add(child);
        }

        AssertPairwiseNonOverlapping(children);
    }

    [Fact]
    public void Resolve_OccupiedExplicitPreferredPosition_FindsAnotherCandidate()
    {
        var run = CreateNode("run", null, ProjectObjectType.ProcessRun, 200d, 200d);
        var occupiedPreferredPosition = (X: 524d, Y: 200d);
        var blocker = CreateNode(
            "blocker",
            null,
            ProjectObjectType.File,
            occupiedPreferredPosition.X,
            occupiedPreferredPosition.Y);
        ProjectObjectRecord[] nodes = [run, blocker];
        var request = CreateRequest(run.NodeKey, ProjectObjectType.File, "Run artifacts") with
        {
            PreferredPosition = occupiedPreferredPosition
        };

        var placement = ProjectStructureAutomaticPlacementPolicy.Resolve(nodes, request);

        Assert.NotEqual(occupiedPreferredPosition, placement);
        Assert.True(placement.X > run.PositionX);
        AssertDoesNotOverlap(placement, request, nodes);
    }

    [Fact]
    public void Resolve_RequiredIncomingLeftDirection_PreservesLeftFacingBranch()
    {
        var definition = CreateNode(
            "definition",
            null,
            ProjectObjectType.ProcessDefinition,
            800d,
            300d);
        var run = CreateNode(
            "run",
            definition.NodeKey,
            ProjectObjectType.ProcessRun,
            400d,
            300d);
        ProjectObjectRecord[] nodes = [definition, run];
        var direction = ProjectStructureAutomaticPlacementPolicy.ResolveIncomingDirection(nodes, run.NodeKey);
        var request = CreateRequest(run.NodeKey, ProjectObjectType.File, "Run artifacts") with
        {
            RequiredDirection = direction
        };

        var placement = ProjectStructureAutomaticPlacementPolicy.Resolve(nodes, request);

        Assert.Equal(ProjectStructurePlacementDirection.Left, direction);
        Assert.True(placement.X < run.PositionX);
        AssertDoesNotOverlap(placement, request, nodes);
    }

    [Fact]
    public void Resolve_CrowdedPreferredSide_SelectsFreeSector()
    {
        var parent = CreateNode("parent", null, ProjectObjectType.ProjectBlock, 400d, 400d);
        var preferredPosition = (X: 736d, Y: 400d);
        var nodes = new List<ProjectObjectRecord> { parent };
        for (var outwardRing = 0; outwardRing <= 2; outwardRing++)
        {
            for (var lane = -5; lane <= 5; lane++)
            {
                nodes.Add(CreateNode(
                    $"right-blocker-{outwardRing}-{lane}",
                    null,
                    ProjectObjectType.File,
                    preferredPosition.X + (outwardRing * 328d),
                    preferredPosition.Y + (lane * 222d)));
            }
        }

        var request = CreateRequest(parent.NodeKey, ProjectObjectType.File, "Generated file") with
        {
            PreferredPosition = preferredPosition
        };

        var placement = ProjectStructureAutomaticPlacementPolicy.Resolve(nodes, request);

        Assert.Equal(parent.PositionX, placement.X);
        Assert.True(placement.Y > parent.PositionY);
        AssertDoesNotOverlap(placement, request, nodes);
    }

    private static ProjectStructureAutomaticPlacementRequest CreateRequest(
        string parentNodeKey,
        ProjectObjectType objectType,
        string title)
        => new(
            parentNodeKey,
            objectType,
            title,
            "Automatically created",
            string.Empty);

    private static ProjectObjectRecord CreateNode(
        string nodeKey,
        string? parentNodeKey,
        ProjectObjectType objectType,
        double x,
        double y,
        string? title = null)
        => new()
        {
            NodeKey = nodeKey,
            ParentNodeKey = parentNodeKey,
            ObjectType = objectType,
            Title = title ?? nodeKey,
            Subtitle = "Existing node",
            PositionX = x,
            PositionY = y
        };

    private static void AssertPairwiseNonOverlapping(IReadOnlyList<ProjectObjectRecord> nodes)
    {
        for (var leftIndex = 0; leftIndex < nodes.Count; leftIndex++)
        {
            var left = ProjectStructureNodeBounds.FromCenter(
                nodes[leftIndex].PositionX,
                nodes[leftIndex].PositionY,
                ProjectStructureNodeGeometry.Estimate(nodes[leftIndex]));
            for (var rightIndex = leftIndex + 1; rightIndex < nodes.Count; rightIndex++)
            {
                var right = ProjectStructureNodeBounds.FromCenter(
                    nodes[rightIndex].PositionX,
                    nodes[rightIndex].PositionY,
                    ProjectStructureNodeGeometry.Estimate(nodes[rightIndex]));

                Assert.False(
                    left.Intersects(right),
                    $"Nodes '{nodes[leftIndex].NodeKey}' and '{nodes[rightIndex].NodeKey}' overlap.");
            }
        }
    }

    private static void AssertDoesNotOverlap(
        (double X, double Y) placement,
        ProjectStructureAutomaticPlacementRequest request,
        IReadOnlyList<ProjectObjectRecord> existingNodes)
    {
        var candidate = ProjectStructureNodeBounds.FromCenter(
            placement.X,
            placement.Y,
            ProjectStructureNodeGeometry.Estimate(
                request.ObjectType,
                request.Title,
                request.Subtitle,
                request.Notes));

        Assert.DoesNotContain(
            existingNodes,
            node => candidate.Intersects(ProjectStructureNodeBounds.FromCenter(
                node.PositionX,
                node.PositionY,
                ProjectStructureNodeGeometry.Estimate(node))));
    }
}
