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

    [Fact]
    public void Expanded_window_can_hide_standard_header_when_custom_surface_owns_the_chrome()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<CanvasFloatingWindow>(
            parameters => parameters
                .Add(component => component.WindowId, "toolbox")
                .Add(component => component.Title, "Toolbox")
                .Add(component => component.ShowHeader, false)
                .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true })
                .Add(component => component.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Body</div>"))));

        Assert.DoesNotContain("cw-floating-window__header", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Body<", cut.Markup);
        context.JSInterop.VerifyInvoke("CanDoItAll.canvasFloatingWindow.create");
    }

    [Fact]
    public async Task Geometry_callback_publishes_committed_window_state_once()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var stateChanges = new List<CanvasWorkbenchWindowState>();
        var cut = context.RenderComponent<CanvasFloatingWindow>(
            parameters => parameters
                .Add(component => component.WindowId, "toolbox")
                .Add(component => component.Title, "Toolbox")
                .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true })
                .Add(component => component.StateChanged, EventCallback.Factory.Create<CanvasWorkbenchWindowState>(this, state => stateChanges.Add(state)))
                .Add(component => component.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Body</div>"))));

        await cut.InvokeAsync(() => cut.Instance.OnGeometryChanged(120.126, 88.624, 430.888, 309.441));

        var state = Assert.Single(stateChanges);
        Assert.True(state.IsVisible);
        Assert.False(state.IsMinimized);
        Assert.Equal(120.13, state.Left);
        Assert.Equal(88.62, state.Top);
        Assert.Equal(430.89, state.Width);
        Assert.Equal(309.44, state.Height);
    }
}
