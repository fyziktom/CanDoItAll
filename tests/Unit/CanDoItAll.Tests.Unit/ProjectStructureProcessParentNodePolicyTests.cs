using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureProcessParentNodePolicyTests
{
    [Fact]
    public void NormalizeCreateParentNodeKey_redirects_current_child_run_node_to_projected_target()
    {
        var context = new ProjectStructureProcessNodeContextDescriptor(
            "process-run:child",
            "process-run:parent",
            "process-run:parent",
            "process-run:parent");

        var normalized = ProjectStructureProcessParentNodePolicy.NormalizeCreateParentNodeKey(
            context,
            "process-run:child");

        Assert.Equal("process-run:parent", normalized);
    }

    [Fact]
    public void NormalizeCreateParentNodeKey_preserves_other_requested_parents()
    {
        var context = new ProjectStructureProcessNodeContextDescriptor(
            "process-run:child",
            "process-run:parent",
            "process-run:parent",
            "process-run:parent");

        var normalized = ProjectStructureProcessParentNodePolicy.NormalizeCreateParentNodeKey(
            context,
            "custom:screenshots");

        Assert.Equal("custom:screenshots", normalized);
    }
}
