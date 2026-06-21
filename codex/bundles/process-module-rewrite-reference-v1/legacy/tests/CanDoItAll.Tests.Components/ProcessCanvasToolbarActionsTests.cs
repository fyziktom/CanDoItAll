using Bunit;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessCanvasToolbarActionsTests
{
    [Fact]
    public void Recomposition_menu_opens_on_hover_and_invokes_the_selected_action()
    {
        using var context = new TestContext();
        var receiver = new object();
        var resolveCollisionsCalls = 0;

        var cut = context.RenderComponent<ProcessCanvasToolbarActions>(parameters => parameters
            .Add(component => component.CanRecomposeCanvas, true)
            .Add(component => component.ResolveCollisionsClicked, EventCallback.Factory.Create(receiver, () => resolveCollisionsCalls++)));

        cut.Find("[data-testid='processes-canvas-recompose-menu']")
            .TriggerEvent("onmouseenter", new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='processes-canvas-recompose-menu-panel']"));
        });

        cut.Find("[data-testid='processes-canvas-recompose-collisions']").Click();

        Assert.Equal(1, resolveCollisionsCalls);
        Assert.Empty(cut.FindAll("[data-testid='processes-canvas-recompose-menu-panel']"));
    }

    [Fact]
    public void Recomposition_menu_stays_closed_when_canvas_cannot_be_recomposed()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<ProcessCanvasToolbarActions>(parameters => parameters
            .Add(component => component.CanRecomposeCanvas, false));

        var toggleButton = cut.Find("[data-testid='processes-canvas-recompose-menu-toggle']");
        Assert.NotNull(toggleButton.GetAttribute("disabled"));

        cut.Find("[data-testid='processes-canvas-recompose-menu']")
            .TriggerEvent("onmouseenter", new MouseEventArgs());

        Assert.Empty(cut.FindAll("[data-testid='processes-canvas-recompose-menu-panel']"));
    }
}
