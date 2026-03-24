using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class FloatingInspectorHostTests
{
    [Fact]
    public void Factory_projects_tab_and_detached_state()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes = [new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha" }],
            UiState = new CanvasWorkbenchUiState { ActiveInspectorTab = "details", IsMaximized = true }
        };

        var snapshot = FloatingInspectorHostFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsDetached);
        Assert.Equal("details", snapshot.ActiveTab);
    }

    [Fact]
    public void Component_renders_floating_inspector_panel()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<FloatingInspectorHost>(
            parameters => parameters.Add(component => component.Snapshot, new FloatingInspectorHostSnapshot
            {
                Title = "Inspector docking is now a named host instead of incidental stage behavior",
                Summary = "Inspector placement stays explicit.",
                StatePill = "Floating",
                Metrics = ["1 selected nodes"],
                InspectorTitle = "Alpha",
                InspectorBody = "Inspector body",
                ActiveTab = "details",
                IsDetached = true
            }));

        Assert.Contains("Inspector docking is now a named host", cut.Markup);
        Assert.Contains("Alpha", cut.Markup);
    }
}
