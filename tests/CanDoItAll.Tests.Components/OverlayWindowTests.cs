using Bunit;
using CanDoItAll.Components.OverlayLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class OverlayWindowTests
{
    [Fact]
    public void Shared_overlay_window_renders_legacy_floating_window_chrome_classes()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<OverlayWindow>(
            parameters => parameters
                .Add(component => component.WindowId, "overlay-toolbox")
                .Add(component => component.Title, "Overlay toolbox")
                .Add(component => component.State, new OverlayWindowState { IsVisible = true })
                .Add(component => component.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Body</div>"))));

        Assert.Contains("cw-floating-window", cut.Markup);
        Assert.Contains("cw-floating-window__header", cut.Markup);
        Assert.Contains("cw-floating-window__drag", cut.Markup);
        Assert.Contains("minimize", cut.Markup);
        context.JSInterop.VerifyInvoke("CanDoItAll.overlayWindow.create");
    }

    [Fact]
    public void Shared_overlay_window_keeps_legacy_expand_icon_when_minimized()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<OverlayWindow>(
            parameters => parameters
                .Add(component => component.WindowId, "overlay-toolbox")
                .Add(component => component.Title, "Overlay toolbox")
                .Add(component => component.State, new OverlayWindowState { IsVisible = true, IsMinimized = true })
                .Add(component => component.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Body</div>"))));

        Assert.Contains("cw-floating-window__chip", cut.Markup);
        Assert.Contains("open_in_full", cut.Markup);
        Assert.Contains("aria-label=\"Expand window\"", cut.Markup);
    }

    [Fact]
    public async Task Shared_overlay_window_geometry_callback_publishes_normalized_state()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var stateChanges = new List<OverlayWindowState>();
        var cut = context.RenderComponent<OverlayWindow>(
            parameters => parameters
                .Add(component => component.WindowId, "overlay-toolbox")
                .Add(component => component.Title, "Overlay toolbox")
                .Add(component => component.State, new OverlayWindowState { IsVisible = true })
                .Add(component => component.StateChanged, EventCallback.Factory.Create<OverlayWindowState>(this, state => stateChanges.Add(state)))
                .Add(component => component.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Body</div>"))));

        await cut.InvokeAsync(() => cut.Instance.OnGeometryChanged(101.249, 72.624, 401.991, 280.336));

        var state = Assert.Single(stateChanges);
        Assert.True(state.IsVisible);
        Assert.Equal(101.25, state.Left);
        Assert.Equal(72.62, state.Top);
        Assert.Equal(401.99, state.Width);
        Assert.Equal(280.34, state.Height);
    }

    [Fact]
    public void Shared_overlay_window_action_buttons_publish_minimize_and_hide_states()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var stateChanges = new List<OverlayWindowState>();
        var cut = context.RenderComponent<OverlayWindow>(
            parameters => parameters
                .Add(component => component.WindowId, "overlay-toolbox")
                .Add(component => component.Title, "Overlay toolbox")
                .Add(component => component.State, new OverlayWindowState { IsVisible = true })
                .Add(component => component.StateChanged, EventCallback.Factory.Create<OverlayWindowState>(this, state => stateChanges.Add(state)))
                .Add(component => component.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Body</div>"))));

        cut.Find("button[aria-label='Minimize window']").Click();
        var minimized = Assert.Single(stateChanges);
        Assert.True(minimized.IsVisible);
        Assert.True(minimized.IsMinimized);

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.State, minimized));

        cut.Find("button[aria-label='Hide window']").Click();
        Assert.Equal(2, stateChanges.Count);
        var hidden = stateChanges[^1];
        Assert.False(hidden.IsVisible);
        Assert.False(hidden.IsMinimized);
    }
}
