using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

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
        Assert.Contains("Right-click menu", cut.Markup);
        context.JSInterop.VerifyInvoke("CanDoItAll.canvasWorkbench.create");
    }

    [Fact]
    public void Workbench_uses_settings_icon_and_marks_settings_overlay_with_toolbar_safe_modifier()
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
            ]
        };

        var cut = context.RenderComponent<CanvasWorkbench>(
            parameters => parameters.Add(component => component.Surface, surface));

        cut.Find("button[aria-label='Toggle settings']").Click();

        Assert.Contains("canvas-settings-overlay", cut.Markup);
        Assert.Contains("cw-help-overlay--settings", cut.Markup);
        Assert.Contains("Canvas settings", cut.Markup);
        Assert.DoesNotContain(">cfg<", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Workbench_help_overlay_supports_context_menu_and_keyboard_pages()
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
            ]
        };

        var cut = context.RenderComponent<CanvasWorkbench>(
            parameters => parameters.Add(component => component.Surface, surface));

        cut.Find("button[aria-label='Toggle help']").Click();
        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Right-click menu", StringComparison.Ordinal))
            .Click();

        Assert.Contains("Press the underlined letter in the active layer", cut.Markup);
        Assert.Contains("Delivery", cut.Markup);
        Assert.Contains("Question", cut.Markup);

        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Keyboard", StringComparison.Ordinal))
            .Click();

        Assert.Contains("shared canvas clipboard", cut.Markup);
        Assert.Contains("layered menu navigation stays fast", cut.Markup);
    }
}


