using CanDoItAll.Modules.Factory.CanvasAdapters;

namespace CanDoItAll.Tests.Components;

public sealed class PromptRunBranchLaneTests
{
    [Fact]
    public void Empty_branch_lane_surfaces_build_annotation_and_actions()
    {
        var node = PromptRunBranchLane.BuildNode("session-root", "main", "Main", 0, 1040, 200);

        Assert.Equal("branch:main", node.Id);
        Assert.Equal("session-root", node.ParentId);
        Assert.Contains(node.Annotations, annotation => annotation.ActionId == "build-flow");
        Assert.Contains(node.ContextActions, action => action.ActionId == "build-flow");
        Assert.Contains(node.ContextActions, action => action.ActionId == "branch-selected");
        Assert.Contains(node.Chips, chip => chip.Text == "Primary");
    }

    [Fact]
    public void Non_primary_step_positions_stagger_downstream_nodes()
    {
        var position = PromptRunBranchLane.ResolveStepPosition("branch-a", 2, 1200, 300);

        Assert.Equal(1200 + 290 + (2 * 238), position.X);
        Assert.Equal(300 + (2 * 84), position.Y);
    }
}
