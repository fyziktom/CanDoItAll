using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureProcessLaunchSourceSnapshotMapperTests
{
    [Fact]
    public void Create_maps_work_items_to_the_typed_launch_source_kind()
    {
        var projectId = Guid.NewGuid();
        var selected = CreateNode(
            "selected",
            ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId),
            ProjectObjectType.WorkItem);
        var surface = new ProjectStructureSurface(
            projectId,
            "TetrisGame",
            [selected],
            [],
            null);

        var context = ProjectStructureProcessLaunchSourceSnapshotMapper.Create(
            surface,
            selected,
            "software-delivery",
            false,
            string.Empty);

        Assert.Equal(ProcessLaunchSourceItemKind.WorkItem, context.Source.SelectedItem.Kind);
        Assert.Equal(
            ProcessLaunchSourceItemKind.WorkItem,
            Assert.Single(context.Source.ContextItems).Kind);
    }

    private static ProjectStructureNode CreateNode(
        string id,
        string? parentId,
        ProjectObjectType objectType)
        => new(
            id,
            parentId,
            objectType,
            "task",
            "Main App",
            string.Empty,
            "Draft",
            "The app must support keyboard play.",
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "task", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
}
