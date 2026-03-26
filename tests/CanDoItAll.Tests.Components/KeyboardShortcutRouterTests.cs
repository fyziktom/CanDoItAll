using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class KeyboardShortcutRouterTests
{
    [Fact]
    public void Factory_includes_clipboard_shortcuts_when_enabled()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Chrome = new CanvasWorkbenchChrome
            {
                Clipboard = new CanvasWorkbenchClipboardOptions
                {
                    IsEnabled = true
                }
            }
        };

        var snapshot = KeyboardShortcutRouterFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha", "beta"]));

        Assert.Equal("Scoped", snapshot.StatePill);
        Assert.Contains("Ctrl/Cmd+C / V clipboard", snapshot.Metrics);
        Assert.Contains("Selection scoped to 2", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_keyboard_shortcut_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<KeyboardShortcutRouter>(
            parameters => parameters.Add(component => component.Snapshot, new KeyboardShortcutRouterSnapshot
            {
                Title = "Shared keyboard routing owns zoom, help, clipboard, and selection scope",
                Summary = "Fit, help, and clipboard now share one router.",
                StatePill = "Scoped",
                IsEnabled = true,
                Metrics = ["0 / +/- fit and zoom", "Ctrl/Cmd+C / V clipboard"]
            }));

        Assert.Contains("Shared keyboard routing owns zoom, help, clipboard, and selection scope", cut.Markup);
        Assert.Contains("Ctrl/Cmd+C / V clipboard", cut.Markup);
    }
}


