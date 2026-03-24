using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class AnimationTimelineTests
{
    [Fact]
    public void Preview_factory_surfaces_viewport_overlay_and_connector_phases()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Links =
            [
                new CanvasWorkbenchLink
                {
                    SourceId = "root",
                    TargetId = "child",
                    IsUserAuthored = true
                }
            ],
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "root",
                    Title = "North star",
                    Family = "root"
                },
                new CanvasWorkbenchNode
                {
                    Id = "child",
                    Title = "Handoff"
                }
            ],
            UiState = new CanvasWorkbenchUiState
            {
                SelectedNodeIds = ["root"]
            }
        };

        var snapshot = AnimationTimelinePreviewFactory.CreateForWorkbench(surface);

        Assert.Equal("Animation timeline", snapshot.Label);
        Assert.Equal(4, snapshot.Phases.Count);
        Assert.Contains(snapshot.Phases, phase => phase.Key == "viewport");
        Assert.Contains(snapshot.Phases, phase => phase.Key == "guides");
        Assert.Contains(snapshot.Metrics, metric => metric.Contains("Reduced motion", StringComparison.Ordinal));
        Assert.Contains(snapshot.Metrics, metric => metric.Contains("authored link", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_component_renders_tracks_and_mounts_preview_interop()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<AnimationTimelinePreview>(
            parameters => parameters.Add(component => component.Snapshot, new AnimationTimelinePreviewSnapshot
            {
                Title = "Shared motion now owns viewport easing, overlay fades, and connector flow",
                Summary = "Workbench focus pans and overlay reveals route through one timeline boundary.",
                StatePill = "Live",
                Metrics = ["Viewport + overlays", "Reduced motion aware"],
                Phases =
                [
                    new AnimationTimelinePhase
                    {
                        Key = "viewport",
                        Label = "Viewport transition",
                        Summary = "Pan and zoom are animated together.",
                        DurationLabel = "320 ms"
                    },
                    new AnimationTimelinePhase
                    {
                        Key = "guides",
                        Label = "Guide fade",
                        Summary = "Snap guides fade in without stealing focus.",
                        DurationLabel = "180 ms"
                    }
                ]
            }));

        Assert.Contains("Animation timeline", cut.Markup);
        Assert.Contains("Viewport transition", cut.Markup);
        Assert.Contains("Reduced motion aware", cut.Markup);
        context.JSInterop.VerifyInvoke("CanDoItAll.animationTimeline.mountPreview");
    }
}
