using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class CanvasWorkbenchTests
{
    [Fact]
    public void Workbench_renders_toolbar_hint_and_help_overlay()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "root",
                    Title = "Root node",
                    X = 120,
                    Y = 160
                }
            ],
            Chrome = new CanvasWorkbenchChrome
            {
                HintText = "Shared canvas hint text",
                QuickCreateActions =
                [
                    new CanvasWorkbenchAction
                    {
                        ActionId = "create-note",
                        Label = "Note"
                    }
                ]
            }
        };

        var cut = context.RenderComponent<CanvasWorkbench>(
            parameters => parameters.Add(component => component.Surface, surface));

        Assert.Contains("Open quick create actions", cut.Markup);
        Assert.Contains("Fit canvas", cut.Markup);
        Assert.Contains("Shared canvas hint text", cut.Markup);

        cut.Find("button[aria-label='Toggle help']").Click();

        Assert.Contains("Interaction vocabulary", cut.Markup);
        context.JSInterop.VerifyInvoke("CanDoItAll.canvasWorkbench.create");
    }
}
