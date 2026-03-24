using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureActionCatalogAdapterTests
{
    [Fact]
    public void Prompt_flow_context_actions_include_wizard_and_create_tools()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("flow", ProjectObjectType.PromptFlow, "Flow", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "wizard");
        Assert.Contains(actions, action => action.ActionId == "progress");
        Assert.Contains(actions, action => action.ActionId == "marker");
        Assert.Contains(actions, action => action.ActionId == "priority");
        Assert.Contains(actions, action => action.ActionId.StartsWith("add-", StringComparison.Ordinal));
    }

    [Fact]
    public void Group_context_actions_expose_border_and_shared_status_tools()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();

        var actions = adapter.BuildGroupContextActions();

        Assert.Contains(actions, action => action.ActionId == "group-frame");
        Assert.Contains(actions, action => action.ActionId == "group-clear-frame");
        Assert.Contains(actions, action => action.ActionId == "progress");
        Assert.Contains(actions, action => action.ActionId == "marker");
        Assert.Contains(actions, action => action.ActionId == "priority");
    }

    private static ProjectStructureNode CreateNode(string id, ProjectObjectType objectType, string title, double x, double y)
        => new(
            id,
            null,
            objectType,
            string.Empty,
            title,
            string.Empty,
            "Draft",
            string.Empty,
            $"/projects/1/{id}",
            title,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            x,
            y,
            new ProjectObjectVisualProfile("rect", "#2563eb", "ID", title),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            0);
}
