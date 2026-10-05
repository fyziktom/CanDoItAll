using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Workbench.Insights.UI.Tests;

public sealed class SupportLifetimeTests {
    [Fact]
    public async Task Inspector_callback_keeps_original_selection_and_receiver_when_render_key_is_unchanged() {
        using var context = CreateContext();
        var first = Origin();
        var second = Origin();
        var original = new List<InsightsSelectionIntent>();
        var successor = new List<InsightsSelectionIntent>();
        var cut = context.Render<ProjectStructureSelectionPanel>(parameters => parameters
            .Add(component => component.State, Selection("a"))
            .Add(component => component.Origin, first)
            .Add(component => component.Dispatch, intent => original.Add(intent)));
        var callback = cut.FindComponents<Button>().Single(button => button.Instance.Class == "project-structure-node-action").Instance.Click;
        cut.Render(parameters => parameters
            .Add(component => component.State, Selection("b"))
            .Add(component => component.Origin, second)
            .Add(component => component.Dispatch, intent => successor.Add(intent)));
        await cut.InvokeAsync(() => callback.InvokeAsync(new()));
        Assert.Empty(successor);
        var action = Assert.Single(original);
        Assert.Equal(first, action.Origin);
        Assert.Equal(["a"], action.NodeIds);
        Assert.Equal(new InsightsSelectionCommand.Inspector("edit"), action.Command);
        await cut.FindComponents<Button>().Single(button => button.Instance.Class == "project-structure-node-action").Instance.Click.InvokeAsync(new());
        Assert.Equal(second, Assert.Single(successor).Origin);
        Assert.Contains("Native task fields", cut.FindComponent<ProjectStructureNodeDetailPreview>().Markup);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Floating_window_and_health_callbacks_keep_rendered_origin_targets_and_receiver() {
        using var context = CreateContext();
        var first = Origin();
        var received = new List<InsightsValidateIntent>();
        var windows = new List<InsightsWindowIntent>();
        var cut = context.Render<ProjectStructureCanvasHealthWindow>(parameters => parameters
            .Add(component => component.Origin, first)
            .Add(component => component.SelectedNodeIds, ["a"])
            .Add(component => component.WindowId, "health")
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true })
            .Add(component => component.SummaryState, new CanDoItAll.Modules.Workbench.CanvasAdapters.ProjectStructureValidationOverlaySummary(true, 1, 2, 3, 1, ["Original issue"]))
            .Add(component => component.CanValidateSelected, true)
            .Add(component => component.ValidateSelected, intent => received.Add(intent))
            .Add(component => component.StateChanged, intent => windows.Add(intent)));
        var validate = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Validate selected").Instance.Click;
        var placement = cut.FindComponent<CanvasFloatingWindow>().Instance.StateChanged;
        cut.Render(parameters => parameters.Add(component => component.Origin, Origin()).Add(component => component.SelectedNodeIds, ["b"]));
        await cut.InvokeAsync(() => validate.InvokeAsync(new()));
        await cut.InvokeAsync(() => placement.InvokeAsync(new CanvasWorkbenchWindowState { IsMinimized = true }));
        Assert.Equal(first, Assert.Single(received).Origin);
        Assert.Equal(["a"], received[0].NodeIds);
        Assert.Equal(first, Assert.Single(windows).Origin);
        Assert.Equal(InsightsWindow.Health, windows[0].Window);
        cut.Render(parameters => parameters.Add(component => component.CanValidateSelected, false));
        Assert.DoesNotContain(cut.FindComponents<Button>(), button => button.Instance.Text == "Validate selected");
        await context.DisposeAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_menu_acknowledgement_cannot_open_after_replacement_or_disposal(bool dispose) {
        using var context = CreateContext();
        var completion = new TaskCompletionSource<InsightsMenu?>(TaskCreationOptions.RunContinuationsAsynchronously);
        InsightsMenuRequest? request = null;
        var cut = context.Render<ProjectStructureObjectIndexWindow>(parameters => parameters
            .Add(component => component.Origin, Origin())
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true })
            .Add(component => component.WindowId, "index")
            .Add(component => component.IsLoaded, true)
            .Add(component => component.Nodes, [new InsightsOutlineNode("a", "Original node", "Ready", "Task", "task", "", "", [])])
            .Add(component => component.OpenMenu, value => {
                request = value;
                return completion.Task;
            }));
        var callback = cut.FindComponent<TreeView>().Instance.OnContextMenu;
        var opening = cut.InvokeAsync(() => callback.InvokeAsync(new TreeViewNodeContextMenuRequest("a", 50, 80)));
        Assert.NotNull(request);
        if (dispose) {
            cut.Instance.Dispose();
        } else {
            cut.Render(parameters => parameters.Add(component => component.Origin, Origin()));
        }
        completion.SetResult(new(request!.Origin, request.OpeningId, "a", "Original node", ["a"], 50, 80, [new("delete", "Delete", "delete", "danger")]));
        await opening;
        Assert.Empty(cut.FindAll("[data-testid=project-structure-outline-context-menu]"));
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Two_selection_instances_do_not_share_render_suppression_or_callbacks() {
        using var context = CreateContext();
        var left = new List<InsightsSelectionIntent>();
        var right = new List<InsightsSelectionIntent>();
        var first = context.Render<ProjectStructureSelectionPanel>(p => p.Add(c => c.State, Selection("left")).Add(c => c.Origin, Origin()).Add(c => c.Dispatch, intent => left.Add(intent)));
        var second = context.Render<ProjectStructureSelectionPanel>(p => p.Add(c => c.State, Selection("right")).Add(c => c.Origin, Origin()).Add(c => c.Dispatch, intent => right.Add(intent)));
        await first.InvokeAsync(() => first.FindComponents<Button>().Single(button => button.Instance.Class == "project-structure-node-action").Instance.Click.InvokeAsync(new()));
        Assert.Equal(["left"], Assert.Single(left).NodeIds);
        Assert.Empty(right);
        Assert.Contains("right", second.Markup);
        await context.DisposeAsync();
    }

    private static InsightsOrigin Origin() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

    private static ProjectStructureSelectionPanelState Selection(string id) => new("same-key", [new(id, id, "Task", "Ready")],
        new(id, id, "Task", "Accepted task", "Ready", "25%", "Normal", "Risk", [], null, null, null, false,
            [new("edit", "Edit", "edit", "neutral")], null, "neutral", true, "Task", "Original project",
            [new("Native task fields", [new("Effort", "3.1256789 hours")])], [new("Original ID", id)]),
        null, "neutral", true, "", false, false);

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
