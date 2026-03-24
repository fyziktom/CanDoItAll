using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class SceneNodeModelPreviewTests
{
    [Fact]
    public void Factory_reports_projected_node_model_metrics()
    {
        var snapshot = SceneNodeModelPreviewFactory.CreateForWorkbench(
            new CanvasWorkbenchSurface
            {
                Nodes =
                [
                    new CanvasWorkbenchNode { Id = "root", Title = "Root", Kind = "Decision", Family = "Artifact", X = 40, Y = 80 },
                    new CanvasWorkbenchNode { Id = "child", ParentId = "root", Title = "Child", Kind = "Note", Family = "Artifact" }
                ]
            },
            SelectionModel.From(["root"]));

        Assert.Contains("Root", snapshot.Metrics);
        Assert.Contains("1 child ids", snapshot.Metrics);
        Assert.Contains("Decision / Artifact", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_scene_node_model_preview_card()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<SceneNodeModelPreview>(
            parameters => parameters.Add(component => component.Snapshot, new SceneNodeModelPreviewSnapshot
            {
                Title = "Projected workbench nodes now map into a shared scene-node model",
                Summary = "Bounds and child relationships are normalized.",
                StatePill = "Selected",
                Metrics = ["Escalated dependency", "1 child ids"]
            }));

        Assert.Contains("Projected workbench nodes now map into a shared scene-node model", cut.Markup);
        Assert.Contains("1 child ids", cut.Markup);
    }
}
