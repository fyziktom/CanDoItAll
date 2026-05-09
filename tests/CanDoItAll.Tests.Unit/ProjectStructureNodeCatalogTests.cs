using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureNodeCatalogTests
{
    [Fact]
    public void BuildAgentNodeCatalog_includes_work_task_node_and_all_object_types()
    {
        var catalog = ProjectStructureCanvasCatalog.BuildAgentNodeCatalog();

        var workTask = Assert.Single(
            catalog.Items,
            item => item.ObjectType == ProjectObjectType.WorkItem &&
                    item.ObjectSubtype == "task");

        Assert.Equal("add-work-task", workTask.ActionId);
        Assert.Contains("work task", workTask.Aliases, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(catalog.Guidance, item => item.Contains("objectType WorkItem with objectSubtype task", StringComparison.Ordinal));

        foreach (var objectType in Enum.GetValues<ProjectObjectType>())
        {
            Assert.Contains(catalog.ObjectTypes, item => item.ObjectType == objectType);
        }

        Assert.Contains(
            catalog.LinkKinds,
            item => item.Kind == ProjectObjectLinkKind.DependsOn &&
                    item.Guidance.Contains("source node depends on the target node", StringComparison.OrdinalIgnoreCase));
    }
}
