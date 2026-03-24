using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ConnectorPathPrimitiveTests
{
    [Fact]
    public void Factory_projects_segments_from_surface_links()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "a", X = 0, Y = 0 },
                new CanvasWorkbenchNode { Id = "b", X = 120, Y = 48 }
            ],
            Links = [new CanvasWorkbenchLink { SourceId = "a", TargetId = "b", Kind = "contains" }]
        };

        var snapshot = ConnectorPathPrimitiveFactory.CreateForWorkbench(surface);

        Assert.Single(snapshot.Segments);
        Assert.Contains("1 total links", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_connector_preview()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<ConnectorPathPrimitive>(
            parameters => parameters.Add(component => component.Snapshot, new ConnectorPathPrimitiveSnapshot
            {
                Title = "Relationship paths now have a named connector primitive instead of being routed only inside the monolithic runtime",
                Summary = "Relationship routing stays shared.",
                StatePill = "Routed",
                Metrics = ["2 preview segments"],
                Segments =
                [
                    new ConnectorPathPrimitiveSegment { Label = "Contains", Tone = "accent", StartX = 10, StartY = 10, EndX = 90, EndY = 30 }
                ]
            }));

        Assert.Contains("Relationship paths now have a named connector primitive", cut.Markup);
        Assert.Contains("Contains", cut.Markup);
    }
}
