using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Tests.Components;

public sealed class CanvasWorkbenchTests
{
    [Fact]
    public void Workbench_preserves_live_viewport_when_surface_data_changes()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.update", _ => true).SetResult(true);

        var initialSurface = new CanvasWorkbenchSurface
        {
            SurfaceId = "project-structure:one",
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
            UiState = new CanvasWorkbenchUiState
            {
                Zoom = 0.7,
                PanX = -420,
                PanY = 260,
                SelectedNodeIds = ["root"]
            }
        };
        var cut = context.RenderComponent<CanvasWorkbench>(
            parameters => parameters.Add(component => component.Surface, initialSurface));

        var updatedSurface = new CanvasWorkbenchSurface
        {
            SurfaceId = initialSurface.SurfaceId,
            Nodes =
            [
                .. initialSurface.Nodes,
                new CanvasWorkbenchNode
                {
                    Id = "created",
                    Title = "Created node",
                    X = 760,
                    Y = 420
                }
            ],
            UiState = new CanvasWorkbenchUiState
            {
                Zoom = initialSurface.UiState.Zoom,
                PanX = initialSurface.UiState.PanX,
                PanY = initialSurface.UiState.PanY,
                SelectedNodeIds = ["created"]
            }
        };

        cut.SetParametersAndRender(parameters => parameters.Add(component => component.Surface, updatedSurface));

        var update = Assert.Single(
            context.JSInterop.Invocations,
            invocation => string.Equals(invocation.Identifier, "CanDoItAll.canvasWorkbench.update", StringComparison.Ordinal));

        Assert.True(ReadPreserveViewport(update.Arguments[^1]));

        cut.SetParametersAndRender(parameters => parameters.Add(component => component.Surface, updatedSurface));

        Assert.Single(
            context.JSInterop.Invocations,
            invocation => string.Equals(invocation.Identifier, "CanDoItAll.canvasWorkbench.update", StringComparison.Ordinal));

        var selectionOnlySurface = new CanvasWorkbenchSurface
        {
            SurfaceId = updatedSurface.SurfaceId,
            Nodes = updatedSurface.Nodes,
            UiState = new CanvasWorkbenchUiState
            {
                Zoom = updatedSurface.UiState.Zoom,
                PanX = updatedSurface.UiState.PanX,
                PanY = updatedSurface.UiState.PanY,
                SelectedNodeIds = ["root"]
            }
        };

        cut.SetParametersAndRender(parameters => parameters.Add(component => component.Surface, selectionOnlySurface));

        var selectionUpdate = context.JSInterop.Invocations.Last(
            invocation => string.Equals(invocation.Identifier, "CanDoItAll.canvasWorkbench.update", StringComparison.Ordinal));

        Assert.False(ReadPreserveViewport(selectionUpdate.Arguments[^1]));

        var replacementSurface = new CanvasWorkbenchSurface
        {
            SurfaceId = "project-structure:two",
            Nodes = initialSurface.Nodes,
            UiState = new CanvasWorkbenchUiState
            {
                Zoom = 1,
                PanX = 90,
                PanY = 110,
                SelectedNodeIds = ["root"]
            }
        };

        cut.SetParametersAndRender(parameters => parameters.Add(component => component.Surface, replacementSurface));

        var replacementUpdate = context.JSInterop.Invocations.Last(
            invocation => string.Equals(invocation.Identifier, "CanDoItAll.canvasWorkbench.update", StringComparison.Ordinal));

        Assert.False(ReadPreserveViewport(replacementUpdate.Arguments[^1]));
    }

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
        Assert.Contains(
            context.JSInterop.Invocations,
            invocation => string.Equals(invocation.Identifier, "CanDoItAll.canvasWorkbench.create", StringComparison.Ordinal));
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

    private static bool ReadPreserveViewport(object? options)
    {
        Assert.NotNull(options);
        return JsonSerializer.SerializeToElement(options, options.GetType())
            .GetProperty("PreserveViewport")
            .GetBoolean();
    }
}


