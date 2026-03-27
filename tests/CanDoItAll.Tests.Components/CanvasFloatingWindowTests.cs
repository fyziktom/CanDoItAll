using Bunit;
using CanDoItAll.Components.CanvasLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class CanvasFloatingWindowTests
{
    [Fact]
    public void Expanded_window_renders_icon_only_actions_with_accessible_labels()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<CanvasFloatingWindow>(
            parameters => parameters
                .Add(component => component.WindowId, "toolbox")
                .Add(component => component.Title, "Toolbox")
                .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true })
                .Add(component => component.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Body</div>"))));

        Assert.Contains("fa-window-minimize", cut.Markup);
        Assert.Contains("fa-rotate-left", cut.Markup);
        Assert.Contains("fa-eye-slash", cut.Markup);
        Assert.Contains("aria-label=\"Minimize window\"", cut.Markup);
        Assert.Contains("aria-label=\"Restart window position and size\"", cut.Markup);
        Assert.Contains("aria-label=\"Hide window\"", cut.Markup);
        Assert.DoesNotContain(">Min<", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Reset<", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Hide<", cut.Markup, StringComparison.Ordinal);
        context.JSInterop.VerifyInvoke("CanDoItAll.canvasFloatingWindow.create");
    }

    [Fact]
    public void Minimized_window_renders_expand_and_hide_icons_without_text_labels()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<CanvasFloatingWindow>(
            parameters => parameters
                .Add(component => component.WindowId, "toolbox")
                .Add(component => component.Title, "Toolbox")
                .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true, IsMinimized = true })
                .Add(component => component.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Body</div>"))));

        Assert.Contains("fa-up-right-and-down-left-from-center", cut.Markup);
        Assert.Contains("fa-eye-slash", cut.Markup);
        Assert.Contains("aria-label=\"Expand window\"", cut.Markup);
        Assert.Contains("aria-label=\"Hide window\"", cut.Markup);
        Assert.DoesNotContain(">Open<", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Hide<", cut.Markup, StringComparison.Ordinal);
        context.JSInterop.VerifyInvoke("CanDoItAll.canvasFloatingWindow.create");
    }
}
