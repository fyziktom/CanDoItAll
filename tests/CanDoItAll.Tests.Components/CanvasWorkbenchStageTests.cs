using Bunit;
using Microsoft.AspNetCore.Components;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class CanvasWorkbenchStageTests
{
    [Fact]
    public void Stage_shell_renders_stats_canvas_inspector_and_supporting_panels()
    {
        using var context = new TestContext();
        RenderFragment canvas = builder => builder.AddMarkupContent(0, "<div id='canvas-slot'>Canvas content</div>");
        RenderFragment inspector = builder => builder.AddMarkupContent(0, "<div id='inspector-slot'>Inspector content</div>");
        RenderFragment supporting = builder => builder.AddMarkupContent(0, "<div id='support-slot'>Supporting content</div>");

        var cut = context.RenderComponent<CanvasWorkbenchStage>(
            parameters => parameters
                .Add(component => component.Eyebrow, "Workbench")
                .Add(component => component.Title, "Phase 2 Bundle Validation")
                .Add(component => component.Description, "Shared stage shell proof.")
                .Add(component => component.Stats, new List<CanvasWorkbenchStat>
                {
                    new() { Label = "Nodes", Value = "11", Tone = "accent" },
                    new() { Label = "Links", Value = "10", Tone = "neutral" }
                })
                .Add(component => component.Canvas, canvas)
                .Add(component => component.Inspector, inspector)
                .Add(component => component.Supporting, supporting));

        Assert.Contains("Workbench", cut.Markup);
        Assert.Contains("Phase 2 Bundle Validation", cut.Markup);
        Assert.Contains("Nodes", cut.Markup);
        Assert.Contains("Canvas content", cut.Markup);
        Assert.Contains("Inspector content", cut.Markup);
        Assert.Contains("Supporting content", cut.Markup);
    }
}
