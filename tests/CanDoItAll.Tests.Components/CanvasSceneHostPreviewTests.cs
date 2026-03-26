using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class CanvasSceneHostPreviewTests
{
    [Fact]
    public void Factory_reports_create_and_update_paths()
    {
        var snapshot = CanvasSceneHostPreviewFactory.CreateForWorkbench(new CanvasWorkbenchSurface
        {
            Nodes = [new CanvasWorkbenchNode { Id = "alpha" }]
        });

        Assert.Contains("Create path armed", snapshot.Metrics);
        Assert.Contains("Update path armed", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_canvas_scene_host_preview_card()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<CanvasSceneHostPreview>(
            parameters => parameters.Add(component => component.Snapshot, new CanvasSceneHostPreviewSnapshot
            {
                Title = "Scene host tracks create and update sync without page-specific state flags",
                Summary = "Pending and applied keys are now explicit.",
                StatePill = "Synced",
                Metrics = ["Create path armed", "Update path armed"]
            }));

        Assert.Contains("Scene host tracks create and update sync without page-specific state flags", cut.Markup);
        Assert.Contains("Update path armed", cut.Markup);
    }
}


