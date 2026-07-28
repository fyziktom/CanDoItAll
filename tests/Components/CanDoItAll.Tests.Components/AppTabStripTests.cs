using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class AppTabStripTests : BunitContext
{
    [Fact]
    public void Renders_state_indicators_and_invokes_actions()
    {
        var activated = string.Empty;
        var movedRight = string.Empty;
        var toggledSleep = string.Empty;

        var cut = Render<AppTabStrip>(parameters => parameters
            .Add(component => component.Items,
            [
                new WorkbenchTabState("projects", "Projects", "/projects", IsPinned: true, IsSleeping: true),
                new WorkbenchTabState("test-lab", "Test Lab", "/test-lab")
            ])
            .Add(component => component.ActiveTabId, "projects")
            .Add(component => component.Activate, EventCallback.Factory.Create<string>(this, value => activated = value))
            .Add(component => component.MoveRight, EventCallback.Factory.Create<string>(this, value => movedRight = value))
            .Add(component => component.ToggleSleep, EventCallback.Factory.Create<string>(this, value => toggledSleep = value)));

        Assert.Contains("push_pin", cut.Markup);
        Assert.Contains("bedtime", cut.Markup);
        Assert.Contains("cda-inline-tab--active", cut.Markup);
        Assert.Contains("cda-inline-tab--inactive", cut.Markup);
        Assert.Contains("cda-inline-tab__actions", cut.Markup);
        Assert.DoesNotContain("cda-inline-tab__actions--active", cut.Markup);
        Assert.Contains("app-tab-strip-main-row", cut.Markup);
        Assert.Contains("cda-tab-strip__controls", cut.Markup);
        var activeTabButton = cut.Find("button[role='tab'][aria-selected='true']");
        Assert.Equal("page", activeTabButton.GetAttribute("aria-current"));
        Assert.Contains("Projects", activeTabButton.TextContent, StringComparison.Ordinal);

        cut.Find("button[title='Move right']").Click();
        cut.Find("button[title='Sleep/wake']").Click();
        cut.Find("button").Click();

        Assert.Equal("projects", movedRight);
        Assert.Equal("projects", toggledSleep);
        Assert.Equal("projects", activated);
    }

    [Fact]
    public void Renders_all_tabs_inline_without_overflow_counter()
    {
        var tabs = Enumerable.Range(1, 8)
            .Select(index => new WorkbenchTabState($"tab-{index}", $"Tab {index}", $"/tab-{index}"))
            .ToArray();

        var cut = Render<AppTabStrip>(parameters => parameters
            .Add(component => component.Items, tabs)
            .Add(component => component.ActiveTabId, "tab-1"));

        foreach (var tab in tabs)
        {
            Assert.Contains(tab.Title, cut.Markup);
        }

        Assert.DoesNotContain("+2", cut.Markup);
    }

    [Fact]
    public void Recent_tabs_menu_explains_retention_and_invokes_clear()
    {
        var cleared = false;
        var recentTabs = new[]
        {
            new WorkbenchTabState("projects", "Projects", "/projects"),
            new WorkbenchTabState("settings", "Settings", "/settings")
        };

        var cut = Render<AppTabStrip>(parameters => parameters
            .Add(component => component.Items, [new WorkbenchTabState("dashboard", "Dashboard", "/")])
            .Add(component => component.RecentTabs, recentTabs)
            .Add(component => component.ClearRecent, EventCallback.Factory.Create(this, () => cleared = true)));

        Assert.Contains("Reopen (2)", cut.Markup);
        Assert.Contains($"latest {WorkbenchTabHistoryPolicy.RecentTabCapacity} kept", cut.Markup);

        cut.Find("[data-testid='clear-recent-tabs']").Click();

        Assert.True(cleared);
    }
}


