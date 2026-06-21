using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureSupportPanelsTests
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

        var cut = context.RenderComponent<ProjectStructureSupportPanels>(parameters => parameters
            .Add(component => component.Nodes, nodes)
            .Add(component => component.SelectedNodeIds, ["node-a", "node-b"])
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

        var cut = context.RenderComponent<ProjectStructureSupportPanels>(parameters => parameters
            .Add(component => component.Nodes, nodes)
            .Add(component => component.SelectedNodeIds, ["node-b", "node-c"])
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

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ProjectStructureSupportPanelContextAction CreateDeleteAction()
        => new("delete", "Delete", "delete", "danger");

    private static ProjectStructureNode CreateNode(string id, string title)
        => new(
            id,
            null,
            ProjectObjectType.Note,
            string.Empty,
            title,
            string.Empty,
            "Ready",
            string.Empty,
            "/projects/test/structure",
            "Note",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("pill", "#059669", "NT", "Note"),
            [],
            "none",
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
}
