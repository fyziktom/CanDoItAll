using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Web.Components.Layout;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class MainLayoutTopBarTests : BunitContext
{
    [Fact]
    public void Status_badges_render_only_in_the_hover_panel()
    {
        var cut = Render<MainLayoutTopBar>(parameters => parameters
            .Add(component => component.ActiveWorkspaceTitle, "Delivery Workspace")
            .Add(component => component.ActiveNavigationTitle, "Agents")
            .Add(component => component.ActiveProjectTitle, "Apollo")
            .Add(component => component.OpenedItemCount, 3)
            .Add(component => component.TabCount, 5));

        Assert.DoesNotContain("Interactive Server", cut.Markup);
        Assert.Single(cut.FindAll(".cda-shell-status-trigger"));

        cut.FindComponent<HelpPopover>()
            .Find("div")
            .TriggerEvent("onmouseenter", new MouseEventArgs { ClientX = 1200, ClientY = 32 });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Delivery Workspace", cut.Markup);
            Assert.Contains("Agents", cut.Markup);
            Assert.Contains("Apollo", cut.Markup);
            Assert.Contains("Interactive Server", cut.Markup);
            Assert.Contains("Live items 3", cut.Markup);
            Assert.Contains("Tabs 5", cut.Markup);
        });
    }

    [Fact]
    public void Status_trigger_can_be_pinned_and_dismissed_from_the_keyboard()
    {
        var cut = Render<MainLayoutTopBar>();
        var trigger = cut.Find(".cda-shell-status-trigger");

        Assert.Equal("BUTTON", trigger.TagName);
        Assert.Equal("dialog", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));

        trigger.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", cut.Find(".cda-shell-status-trigger").GetAttribute("aria-expanded"));
            Assert.Contains("Workspace status", cut.Find("[data-testid='layout-status-popover']").TextContent);
        });

        cut.Find(".cda-shell-status-trigger")
            .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find(".cda-shell-status-trigger").GetAttribute("aria-expanded"));
            Assert.Empty(cut.FindAll("[data-testid='layout-status-popover']"));
        });
    }
}
