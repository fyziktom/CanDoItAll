using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Workbench.Insights.UI;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Workbench.Insights.UI.Tests;

public sealed class ProjectStructureObjectIndexWindowTests
{
    [Fact]
    public async Task Outline_context_delete_targets_selected_nodes_when_clicked_node_is_selected()
    {
        using var context = CreateContext();
        var nodes = new[]
        {
            CreateNode("node-a", "Architecture"),
            CreateNode("node-b", "Implementation")
        };
        InsightsMenuIntent? actionRequest = null;

        var cut = context.Render<ProjectStructureObjectIndexWindow>(parameters => AddRequiredParameters(
                parameters,
                nodes,
                selectedNodeIds: ["node-a", "node-b"])
            .Add(component => component.OpenMenu, request => Task.FromResult<InsightsMenu?>(new(request.Origin, request.OpeningId, request.NodeId, "2 selected nodes", ["node-a", "node-b"], request.ClientX, request.ClientY, [CreateDeleteAction()])))
            .Add(
                component => component.OnExecuteNodeContextAction,
                EventCallback.Factory.Create<InsightsMenuIntent>(
                    new object(),
                    request => actionRequest = request)));

        await cut.Find("[data-testid='project-structure-outline-node-node-a']")
            .TriggerEventAsync("oncontextmenu", new MouseEventArgs { ClientX = 140, ClientY = 96 });

        Assert.Contains("2 selected nodes", cut.Markup);

        await cut.Find("[data-testid='project-structure-outline-context-action-delete']").ClickAsync(new());

        Assert.NotNull(actionRequest);
        Assert.Equal("node-a", actionRequest!.Menu.NodeId);
        Assert.Equal("delete", actionRequest.ActionId);
        Assert.Equal(new[] { "node-a", "node-b" }, actionRequest.Menu.NodeIds);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Outline_context_menu_selects_clicked_node_when_it_is_not_in_multi_selection()
    {
        using var context = CreateContext();
        var nodes = new[]
        {
            CreateNode("node-a", "Architecture"),
            CreateNode("node-b", "Implementation"),
            CreateNode("node-c", "Validation")
        };
        var selectedNodeId = string.Empty;
        InsightsMenuIntent? actionRequest = null;

        var cut = context.Render<ProjectStructureObjectIndexWindow>(parameters => AddRequiredParameters(
                parameters,
                nodes,
                selectedNodeIds: ["node-b", "node-c"])
            .Add(component => component.OpenMenu, request => {
                selectedNodeId = request.NodeId;
                return Task.FromResult<InsightsMenu?>(new(request.Origin, request.OpeningId, request.NodeId, "Architecture", [request.NodeId], request.ClientX, request.ClientY, [CreateDeleteAction()]));
            })
            .Add(
                component => component.OnSelectNode,
                EventCallback.Factory.Create<InsightsFocusIntent>(
                    new object(),
                    request => selectedNodeId = request.NodeId))
            .Add(
                component => component.OnExecuteNodeContextAction,
                EventCallback.Factory.Create<InsightsMenuIntent>(
                    new object(),
                    request => actionRequest = request)));

        await cut.Find("[data-testid='project-structure-outline-node-node-a']")
            .TriggerEventAsync("oncontextmenu", new MouseEventArgs { ClientX = 140, ClientY = 96 });
        await cut.Find("[data-testid='project-structure-outline-context-action-delete']").ClickAsync(new());

        Assert.Equal("node-a", selectedNodeId);
        Assert.NotNull(actionRequest);
        Assert.Equal(new[] { "node-a" }, actionRequest!.Menu.NodeIds);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Search_filters_visible_nodes_by_title_status_and_type()
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
                EventCallback.Factory.Create<InsightsSearchIntent>(
                    new object(),
                    request => searchText = request.Text)));

        Assert.Contains("Architecture", cut.Markup);
        Assert.DoesNotContain("Implementation", cut.Markup);

        await cut.Find("[data-testid='project-structure-object-index-search']")
            .InputAsync(new() { Value = "work item" });

        Assert.Equal("work item", searchText);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Loaded_window_renders_owned_tree_scroller()
    {
        using var context = CreateContext();

        var cut = context.Render<ProjectStructureObjectIndexWindow>(
            parameters => AddRequiredParameters(
                parameters,
                Enumerable.Range(0, 12)
                    .Select(index => CreateNode($"node-{index}", $"Node {index}"))
                    .ToList()));

        Assert.NotNull(cut.Find("[data-testid='project-structure-object-index-tree-scroller']"));
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Unloaded_window_does_not_render_node_index()
    {
        using var context = CreateContext();

        var cut = context.Render<ProjectStructureObjectIndexWindow>(parameters => AddRequiredParameters(
            parameters,
            [CreateNode("node-a", "Architecture")],
            isLoaded: false));

        Assert.Contains("Object index loading is paused.", cut.Markup);
        Assert.DoesNotContain("project-structure-outline-node-node-a", cut.Markup);
        await context.DisposeAsync();
    }

    private static ComponentParameterCollectionBuilder<ProjectStructureObjectIndexWindow> AddRequiredParameters(
        ComponentParameterCollectionBuilder<ProjectStructureObjectIndexWindow> parameters,
        IReadOnlyList<InsightsOutlineNode> nodes,
        IReadOnlyList<string>? selectedNodeIds = null,
        bool isLoaded = true)
    {
        return parameters
            .Add(component => component.Origin, new InsightsOrigin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1))
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

    private static InsightsOutlineNode CreateNode(string id, string title, string status = "Ready")
        => new(id, title, status, "Work item", "task_alt", string.Empty, "task", [CreateDeleteAction()]);
}
