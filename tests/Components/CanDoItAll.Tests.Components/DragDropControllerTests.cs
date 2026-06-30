using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class DragDropControllerTests
{
    [Fact]
    public void Factory_reports_drag_metrics_for_selected_nodes_and_drop_actions()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha" },
                new CanvasWorkbenchNode { Id = "beta", Title = "Beta" }
            ],
            UiState = new CanvasWorkbenchUiState
            {
                GroupFrames =
                [
                    new CanvasWorkbenchGroupFrame { Id = "frame-a", Label = "Frame" }
                ]
            },
            Chrome = new CanvasWorkbenchChrome
            {
                QuickCreateActions =
                [
                    new CanvasWorkbenchAction { ActionId = "upload", Label = "Upload", RequiresFile = true },
                    new CanvasWorkbenchAction { ActionId = "note", Label = "Note", SupportsDragDrop = false }
                ]
            }
        };

        var snapshot = DragDropControllerFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsEnabled);
        Assert.Contains("2 draggable nodes", snapshot.Metrics);
        Assert.Contains("1 selected move set", snapshot.Metrics);
        Assert.Contains("1 drop-capable actions", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_drag_drop_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<DragDropController>(
            parameters => parameters.Add(component => component.Snapshot, new DragDropControllerSnapshot
            {
                Title = "Drag lifecycle owns node moves, grouped drags, and drop-capable create actions",
                Summary = "Pointer capture and move sets stay shared.",
                StatePill = "Active",
                IsEnabled = true,
                Metrics = ["11 draggable nodes", "1 selected move set"]
            }));

        Assert.Contains("Drag lifecycle owns node moves", cut.Markup);
        Assert.Contains("11 draggable nodes", cut.Markup);
    }
}


