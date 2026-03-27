using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePlacementPolicyTests
{
    [Fact]
    public void Sibling_placement_stacks_below_the_source()
    {
        var sourceNode = CreateNode("source", "parent", 300, 200);
        var siblingNode = sourceNode with { Id = "sibling" };
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-note",
            "source",
            0,
            0,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            "sibling",
            "command",
            string.Empty,
            null);

        var placement = new ProjectStructurePlacementPolicy().ResolveCreatePlacement([sourceNode, siblingNode], sourceNode, null, request);

        Assert.Equal(300, placement.X);
        Assert.Equal(332, placement.Y);
    }

    [Fact]
    public void Parent_resolution_prefers_explicit_parent_then_source()
    {
        var sourceNode = CreateNode("source", "parent", 300, 200);
        var childRequest = new CanvasWorkbenchCreateActionRequest(
            "add-note",
            "source",
            0,
            0,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            "child",
            "command",
            string.Empty,
            null);
        var explicitRequest = childRequest with { ParentNodeId = "explicit-parent" };

        Assert.Equal("explicit-parent", ProjectStructurePlacementPolicy.ResolveParentNodeId(sourceNode, explicitRequest));
        Assert.Equal("source", ProjectStructurePlacementPolicy.ResolveParentNodeId(sourceNode, childRequest));
    }

    [Fact]
    public void Child_placement_follows_the_anchor_side_relative_to_its_parent()
    {
        var parentNode = CreateNode("parent", null, 620, 260);
        var leftBranch = CreateNode("left-branch", "parent", 320, 260);
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-note",
            "left-branch",
            0,
            0,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            "child",
            "command",
            string.Empty,
            null);

        var placement = new ProjectStructurePlacementPolicy().ResolveCreatePlacement([parentNode, leftBranch], leftBranch, leftBranch, request);

        Assert.NotNull(placement.X);
        Assert.NotNull(placement.Y);
        Assert.True(placement.X < leftBranch.X);
    }

    private static ProjectStructureNode CreateNode(string id, string? parentId, double x, double y)
        => new(
            id,
            parentId,
            ProjectObjectType.Note,
            string.Empty,
            "Node",
            string.Empty,
            "Draft",
            string.Empty,
            "/projects/1/structure",
            "Note",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            x,
            y,
            new ProjectObjectVisualProfile("pill", "#059669", "NT", "Note"),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            0);
}


