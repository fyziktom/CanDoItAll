using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class CreateActionPaletteTests
{
    [Fact]
    public void Factory_projects_quick_and_group_actions()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Chrome = new CanvasWorkbenchChrome
            {
                QuickCreateActions = [new CanvasWorkbenchAction { ActionId = "note", Label = "Note", RequiresInput = true }],
                GroupContextActions = [new CanvasWorkbenchAction { ActionId = "frame", Label = "Frame" }]
            }
        };

        var snapshot = CreateActionPaletteFactory.CreateForWorkbench(surface);

        Assert.Single(snapshot.QuickActions);
        Assert.Single(snapshot.GroupActions);
        Assert.Contains("1 input-driven actions", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_create_palette_sections()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<CreateActionPalette>(
            parameters => parameters.Add(component => component.Snapshot, new CreateActionPaletteSnapshot
            {
                Title = "Shared create flows now project through one palette boundary instead of piggybacking on menu internals",
                Summary = "Create actions stay shared.",
                StatePill = "Armed",
                Metrics = ["2 quick create actions"],
                QuickActions = [new CanvasWorkbenchAction { ActionId = "note", Label = "Note", Tone = "accent" }],
                GroupActions = [new CanvasWorkbenchAction { ActionId = "frame", Label = "Frame", Tone = "info" }]
            }));

        Assert.Contains("Shared create flows now project through one palette boundary", cut.Markup);
        Assert.Contains("Quick create", cut.Markup);
    }
}
