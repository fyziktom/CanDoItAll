using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class AccessibilityMirrorLayerTests
{
    [Fact]
    public void Workbench_factory_prioritizes_selected_items_in_the_mirror_snapshot()
    {
        var surface = new CanvasWorkbenchSurface
        {
            SurfaceId = "test-surface",
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha node", Subtitle = "Selected" },
                new CanvasWorkbenchNode { Id = "beta", Title = "Beta node", Subtitle = "Secondary" }
            ],
            UiState = new CanvasWorkbenchUiState
            {
                SelectedNodeIds = ["alpha"]
            }
        };

        var snapshot = AccessibilityMirrorLayerFactory.CreateForWorkbench(surface, enableDiagnostics: true);

        Assert.True(snapshot.EnableDiagnostics);
        Assert.Equal("alpha", snapshot.Items.First().Id);
        Assert.Contains("primary selection", snapshot.LiveAnnouncement, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Component_renders_the_hidden_semantic_region_and_debug_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var snapshot = new AccessibilityMirrorSnapshot
        {
            Id = "mirror",
            SummaryLabel = "2 mirrored canvas nodes",
            LiveAnnouncement = "Alpha node is the primary selection.",
            EnableDiagnostics = true,
            Items =
            [
                new AccessibilityMirrorItem
                {
                    Id = "alpha",
                    Label = "Alpha node",
                    Description = "Primary selection",
                    IsSelected = true,
                    IsPrimary = true
                },
                new AccessibilityMirrorItem
                {
                    Id = "beta",
                    Label = "Beta node",
                    Description = "Secondary"
                }
            ]
        };

        var cut = context.RenderComponent<AccessibilityMirrorLayer>(
            parameters => parameters.Add(component => component.Snapshot, snapshot));

        Assert.Contains("2 mirrored canvas nodes", cut.Markup);
        Assert.Contains("Alpha node", cut.Markup);
        Assert.Single(cut.FindAll("[data-testid='accessibility-mirror-debug']"));
    }
}


