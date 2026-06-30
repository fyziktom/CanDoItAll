using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class DiagnosticsOverlayTests
{
    [Fact]
    public void Factory_marks_the_snapshot_visible_when_diagnostics_are_enabled()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes = [new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha" }],
            Links = [new CanvasWorkbenchLink { SourceId = "alpha", TargetId = "alpha", Kind = "self" }],
            UiState = new CanvasWorkbenchUiState { ShowDiagnostics = true },
            Chrome = new CanvasWorkbenchChrome
            {
                Diagnostics = new CanvasWorkbenchDiagnosticsOptions
                {
                    IsEnabled = true,
                    ShowConnectorAnchors = true,
                    ShowNodeBounds = true,
                    ShowViewportStats = true
                }
            }
        };

        var snapshot = DiagnosticsOverlayFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsVisible);
        Assert.Contains("Anchor hints", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_the_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<DiagnosticsOverlay>(
            parameters => parameters.Add(component => component.Snapshot, new DiagnosticsOverlaySnapshot
            {
                Title = "Diagnostics are live on the workbench",
                Summary = "Live bounds and anchors are visible.",
                StatePill = "Live",
                Metrics = ["2 nodes", "1 selected"]
            }));

        Assert.Contains("Diagnostics are live on the workbench", cut.Markup);
        Assert.Contains("1 selected", cut.Markup);
    }
}


