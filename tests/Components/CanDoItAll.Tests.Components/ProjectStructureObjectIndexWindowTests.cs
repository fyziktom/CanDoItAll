using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureObjectIndexWindowTests
{
    [Fact]
    public void Outline_context_delete_targets_selected_nodes_when_clicked_node_is_selected()
    {
        using var context = CreateContext();
        var nodes = new[]
        {
            CreateNode("node-a", "Architecture"),
            CreateNode("node-b", "Implementation")
        };
        ProjectStructureSupportPanelContextActionRequest? actionRequest = null;

        var cut = context.Render<ProjectStructureObjectIndexWindow>(parameters => AddRequiredParameters(
                parameters,
                nodes,
                selectedNodeIds: ["node-a", "node-b"])
            .Add(component => component.ResolveContextActions, _ => [CreateDeleteAction()])
            .Add(
                component => component.OnExecuteNodeContextAction,
                EventCallback.Factory.Create<ProjectStructureSupportPanelContextActionRequest>(
                    new object(),
                    request => actionRequest = request)));

        cut.Find("[data-testid='project-structure-outline-node-node-a']")
            .TriggerEvent("oncontextmenu", new MouseEventArgs { ClientX = 140, ClientY = 96 });

        Assert.Contains("2 selected nodes", cut.Markup);

        cut.Find("[data-testid='project-structure-outline-context-action-delete']").Click();

        Assert.NotNull(actionRequest);
        Assert.Equal("node-a", actionRequest!.NodeId);
        Assert.Equal("delete", actionRequest.ActionId);
        Assert.Equal(new[] { "node-a", "node-b" }, actionRequest.TargetNodeIds);
    }

    [Fact]
    public void Outline_context_menu_selects_clicked_node_when_it_is_not_in_multi_selection()
    {
        using var context = CreateContext();
        var nodes = new[]
        {
            CreateNode("node-a", "Architecture"),
            CreateNode("node-b", "Implementation"),
            CreateNode("node-c", "Validation")
        };
        var selectedNodeId = string.Empty;
        ProjectStructureSupportPanelContextActionRequest? actionRequest = null;

        var cut = context.Render<ProjectStructureObjectIndexWindow>(parameters => AddRequiredParameters(
                parameters,
                nodes,
                selectedNodeIds: ["node-b", "node-c"])
            .Add(component => component.ResolveContextActions, _ => [CreateDeleteAction()])
            .Add(
                component => component.OnSelectNode,
                EventCallback.Factory.Create<string>(
                    new object(),
                    nodeId => selectedNodeId = nodeId))
            .Add(
                component => component.OnExecuteNodeContextAction,
                EventCallback.Factory.Create<ProjectStructureSupportPanelContextActionRequest>(
                    new object(),
                    request => actionRequest = request)));

        cut.Find("[data-testid='project-structure-outline-node-node-a']")
            .TriggerEvent("oncontextmenu", new MouseEventArgs { ClientX = 140, ClientY = 96 });
        cut.Find("[data-testid='project-structure-outline-context-action-delete']").Click();

        Assert.Equal("node-a", selectedNodeId);
        Assert.NotNull(actionRequest);
        Assert.Equal(new[] { "node-a" }, actionRequest!.TargetNodeIds);
    }

    [Fact]
    public void Search_filters_visible_nodes_by_title_status_and_type()
    {
        using var context = CreateContext();
        var searchText = "ready";
        var nodes = new[]
        {
            CreateNode("node-a", "Architecture", "Ready"),
            CreateNode("node-b", "Implementation", "Blocked")
        };

        var cut = context.Render<ProjectStructureObjectIndexWindow>(parameters => AddRequiredParameters(parameters, nodes)
            .Add(component => component.SearchText, searchText)
            .Add(
                component => component.SearchTextChanged,
                EventCallback.Factory.Create<string>(
                    new object(),
                    value => searchText = value)));

        Assert.Contains("Architecture", cut.Markup);
        Assert.DoesNotContain("Implementation", cut.Markup);

        cut.Find("[data-testid='project-structure-object-index-search']")
            .Input("work item");

        Assert.Equal("work item", searchText);
    }

    [Fact]
    public void Loaded_window_renders_owned_tree_scroller()
    {
        using var context = CreateContext();

        var cut = context.Render<ProjectStructureObjectIndexWindow>(
            parameters => AddRequiredParameters(
                parameters,
                Enumerable.Range(0, 12)
                    .Select(index => CreateNode($"node-{index}", $"Node {index}"))
                    .ToList()));

        Assert.NotNull(cut.Find("[data-testid='project-structure-object-index-tree-scroller']"));
    }

    [Fact]
    public void Unloaded_window_does_not_render_node_index()
    {
        using var context = CreateContext();

        var cut = context.Render<ProjectStructureObjectIndexWindow>(parameters => AddRequiredParameters(
            parameters,
            [CreateNode("node-a", "Architecture")],
            isLoaded: false));

        Assert.Contains("Object index loading is paused.", cut.Markup);
        Assert.DoesNotContain("project-structure-outline-node-node-a", cut.Markup);
    }

    private static ComponentParameterCollectionBuilder<ProjectStructureObjectIndexWindow> AddRequiredParameters(
        ComponentParameterCollectionBuilder<ProjectStructureObjectIndexWindow> parameters,
        IReadOnlyList<ProjectStructureNode> nodes,
        IReadOnlyList<string>? selectedNodeIds = null,
        bool isLoaded = true)
    {
        return parameters
            .Add(component => component.WindowId, "project-structure.objectIndex")
            .Add(component => component.TestId, "project-structure-object-index-window")
            .Add(component => component.AriaLabel, "Project object index")
            .Add(component => component.Kicker, "Object index")
            .Add(component => component.Title, "Project object index")
            .Add(component => component.Summary, "2 nodes")
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true })
            .Add(component => component.IsLoaded, isLoaded)
            .Add(component => component.Nodes, nodes)
            .Add(component => component.SelectedNodeIds, selectedNodeIds ?? []);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ProjectStructureSupportPanelContextAction CreateDeleteAction()
        => new("delete", "Delete", "delete", "danger");

    private static ProjectStructureNode CreateNode(string id, string title, string status = "Ready")
        => new(
            id,
            null,
            ProjectObjectType.WorkItem,
            string.Empty,
            title,
            string.Empty,
            status,
            string.Empty,
            "/projects/test/structure",
            "Work item",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("pill", "#059669", "WI", "Work item"),
            [],
            "none",
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
}
