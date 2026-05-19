using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePlacementPolicyTests
{
    [Fact]
    public void Sibling_placement_stacks_close_below_the_source()
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
        Assert.Equal(312, placement.Y);
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

    [Fact]
    public void Standard_child_placement_uses_nearby_space_before_falling_back_to_wide_offsets()
    {
        var sourceNode = CreateNode("source", "parent", 300, 200);
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-block-feature",
            "source",
            sourceNode.X,
            sourceNode.Y,
            sourceNode.Id,
            string.Empty,
            string.Empty,
            string.Empty,
            "child",
            "dialog",
            "feature",
            null);

        var placement = new ProjectStructurePlacementPolicy().ResolveCreatePlacement(
            [sourceNode],
            sourceNode,
            sourceNode,
            request,
            ProjectObjectType.ProjectBlock);

        Assert.NotNull(placement.X);
        Assert.True(placement.X > sourceNode.X);
        Assert.InRange(placement.X.Value - sourceNode.X, 260d, 320d);
        Assert.Equal(sourceNode.Y, placement.Y);
    }

    [Fact]
    public void Simple_note_child_placement_sits_right_next_to_the_source()
    {
        var sourceNode = CreateNode("source", "parent", 300, 200);
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-note",
            "source",
            sourceNode.X,
            sourceNode.Y,
            sourceNode.Id,
            "Child note",
            string.Empty,
            "Child note",
            "child",
            "quick-note",
            string.Empty,
            null);

        var plan = new ProjectStructurePlacementPolicy().ResolveCreatePlacementPlan(
            [sourceNode],
            sourceNode,
            sourceNode,
            request,
            ProjectObjectType.Note);

        Assert.Equal(488, plan.Placement.X);
        Assert.Equal(sourceNode.Y, plan.Placement.Y);
        Assert.Empty(plan.FollowUpMoves);
    }

    [Fact]
    public void Simple_note_child_placement_follows_left_facing_parent_branch()
    {
        var parentNode = CreateNode("parent", null, 620, 260);
        var leftBranch = CreateNode("left-branch", "parent", 320, 260);
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-note",
            "left-branch",
            leftBranch.X,
            leftBranch.Y,
            leftBranch.Id,
            "Child note",
            string.Empty,
            "Child note",
            "child",
            "quick-note",
            string.Empty,
            null);

        var plan = new ProjectStructurePlacementPolicy().ResolveCreatePlacementPlan(
            [parentNode, leftBranch],
            leftBranch,
            leftBranch,
            request,
            ProjectObjectType.Note);

        Assert.NotNull(plan.Placement.X);
        Assert.True(plan.Placement.X < leftBranch.X);
        Assert.Equal(leftBranch.Y, plan.Placement.Y);
        Assert.Empty(plan.FollowUpMoves);
    }

    [Fact]
    public void Simple_note_sibling_placement_moves_lower_stack_nodes_down()
    {
        var sourceNode = CreateNode("source", "parent", 300, 200, notes: "Source note");
        var lowerNode = CreateNode("lower", "parent", 300, 304, notes: "Lower note");
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-note",
            "source",
            sourceNode.X,
            sourceNode.Y,
            sourceNode.ParentId,
            "Inserted note\r\nwith more content\r\nthat needs room",
            string.Empty,
            "Inserted note\r\nwith more content\r\nthat needs room",
            "sibling",
            "quick-note",
            string.Empty,
            null);

        var plan = new ProjectStructurePlacementPolicy().ResolveCreatePlacementPlan(
            [sourceNode, lowerNode],
            sourceNode,
            null,
            request,
            ProjectObjectType.Note);

        var move = Assert.Single(plan.FollowUpMoves);
        Assert.Equal(300, plan.Placement.X);
        Assert.True(plan.Placement.Y > sourceNode.Y);
        Assert.Equal(lowerNode.Id, move.NodeId);
        Assert.True(move.Y > lowerNode.Y);
    }

    private static ProjectStructureNode CreateNode(string id, string? parentId, double x, double y, string notes = "")
        => new(
            id,
            parentId,
            ProjectObjectType.Note,
            string.Empty,
            "Node",
            string.Empty,
            "Draft",
            notes,
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
            [],
            0);
}


