using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class AppTabStripTests : BunitContext
{
    public AppTabStripTests() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var tabModule = JSInterop.SetupModule("./_content/CanDoItAll.AppComponents/js/appTabStrip.js");
        tabModule.Setup<double[]>("getMenuPosition", _ => true).SetResult([180, 40]);
        JSInterop.SetupModule("./_content/CanDoItAll.Components.BaseLib/Components/Navigation/ContextMenu.razor.js");
    }
    [Fact]
    public async Task Renders_state_indicators_and_invokes_actions()
    {
        var activated = string.Empty;
        var movedLeft = string.Empty;
        var movedRight = string.Empty;
        var toggledPin = string.Empty;
        var toggledSleep = string.Empty;
        var closed = string.Empty;
        var closedOthers = string.Empty;
        var closedRight = string.Empty;

        var cut = Render<AppTabStrip>(parameters => parameters
            .Add(component => component.Items,
            [
                new WorkbenchTabState("dashboard", "Dashboard", "/", CanClose: false),
                new WorkbenchTabState("projects", "Projects", "/projects", IsPinned: true, IsSleeping: true),
                new WorkbenchTabState("test-lab", "Test Lab", "/test-lab")
            ])
            .Add(component => component.ActiveTabId, "projects")
            .Add(component => component.Activate, EventCallback.Factory.Create<string>(this, value => activated = value))
            .Add(component => component.MoveLeft, EventCallback.Factory.Create<string>(this, value => movedLeft = value))
            .Add(component => component.MoveRight, EventCallback.Factory.Create<string>(this, value => movedRight = value))
            .Add(component => component.TogglePin, EventCallback.Factory.Create<string>(this, value => toggledPin = value))
            .Add(component => component.ToggleSleep, EventCallback.Factory.Create<string>(this, value => toggledSleep = value))
            .Add(component => component.Close, EventCallback.Factory.Create<string>(this, value => closed = value))
            .Add(component => component.CloseOthers, EventCallback.Factory.Create<string>(this, value => closedOthers = value))
            .Add(component => component.CloseRight, EventCallback.Factory.Create<string>(this, value => closedRight = value)));

        Assert.Contains("push_pin", cut.Markup);
        Assert.Contains("bedtime", cut.Markup);
        Assert.Contains("app-inline-tab--active", cut.Markup);
        Assert.Contains("app-inline-tab--inactive", cut.Markup);
        Assert.Contains("app-inline-tab__actions", cut.Markup);
        Assert.DoesNotContain("app-inline-tab__actions--active", cut.Markup);
        Assert.Contains("app-tab-strip-main-row", cut.Markup);
        Assert.Contains("app-tab-strip__controls", cut.Markup);
        var activeTabButton = cut.Find("button[role='tab'][aria-selected='true']");
        Assert.Equal("page", activeTabButton.GetAttribute("aria-current"));
        Assert.Contains("Projects", activeTabButton.TextContent, StringComparison.Ordinal);
        Assert.Single(activeTabButton.QuerySelectorAll(".app-inline-tab__sleep-state"));
        Assert.Empty(activeTabButton.QuerySelectorAll("button"));
        Assert.Equal(3, cut.FindAll("[data-tab-id='projects'] button").Count);
        Assert.Empty(cut.FindAll("[data-tab-id='dashboard'] button[title='Close tab']"));

        activeTabButton.Click();
        Assert.Equal("projects", activated);
        activated = string.Empty;

        await ChooseActionAsync("Move left");
        await ChooseActionAsync("Move right");
        await ChooseActionAsync("Unpin tab");
        await ChooseActionAsync("Wake tab");
        await ChooseActionAsync("Close other tabs");
        await ChooseActionAsync("Close tabs to the right");
        cut.Find("[data-tab-id='projects'] button[title='Close tab']").Click();

        Assert.Equal("projects", movedLeft);
        Assert.Equal("projects", movedRight);
        Assert.Equal("projects", toggledPin);
        Assert.Equal("projects", toggledSleep);
        Assert.Equal("projects", closed);
        Assert.Equal("projects", closedOthers);
        Assert.Equal("projects", closedRight);
        Assert.Equal(string.Empty, activated);

        cut.Render(parameters => parameters.Add(component => component.Items,
        [
            new WorkbenchTabState("projects", "Projects", "/projects"),
            new WorkbenchTabState("test-lab", "Test Lab", "/test-lab", CanSleep: false)
        ]));
        Assert.Empty(cut.FindAll(".app-inline-tab__sleep-state"));
        await cut.Find("[data-tab-id='projects'] button[title='Tab actions']").ClickAsync(new());
        Assert.True(cut.FindAll("[role='menuitem']").Single(button => button.TextContent.Trim().EndsWith("Sleep tab", StringComparison.Ordinal)).HasAttribute("disabled"));
        cut.Render(parameters => parameters.Add(component => component.ActiveTabId, "test-lab"));
        var sleep = cut.FindAll("[role='menuitem']").Single(button => button.TextContent.Trim().EndsWith("Sleep tab", StringComparison.Ordinal));
        Assert.False(sleep.HasAttribute("disabled"));
        toggledSleep = string.Empty;
        await sleep.ClickAsync(new());
        Assert.Equal("projects", toggledSleep);
        await cut.Find("[data-tab-id='test-lab'] button[title='Tab actions']").ClickAsync(new());
        cut.Render(parameters => parameters.Add(component => component.ActiveTabId, "projects"));
        Assert.True(cut.FindAll("[role='menuitem']").Single(button => button.TextContent.Trim().EndsWith("Sleep tab", StringComparison.Ordinal)).HasAttribute("disabled"));
        await cut.Find("[role='menu']").TriggerEventAsync("onkeydown", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        await cut.Find("[data-tab-id='projects'] button[title='Tab actions']").ClickAsync(new());
        cut.Render(parameters => parameters.Add(component => component.Items,
            [new WorkbenchTabState("dashboard", "Dashboard", "/", CanClose: false)]));
        Assert.Empty(cut.FindAll("[role='menu']"));

        async Task ChooseActionAsync(string label) {
            await cut.Find("[data-tab-id='projects'] button[title='Tab actions']").ClickAsync(new());
            var menu = cut.FindComponent<ContextMenu>();
            Assert.Equal(180, menu.Instance.X);
            Assert.Equal(40, menu.Instance.Y);
            await cut.FindAll("[role='menuitem']")
                .Single(button => button.TextContent.Trim().EndsWith(label, StringComparison.Ordinal))
                .ClickAsync(new());
            Assert.Empty(cut.FindAll("[role='menu']"));
        }
    }

    [Fact]
    public void Keeps_all_tabs_reachable_in_scroll_row_and_menu()
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

        Assert.Equal(8, cut.FindAll("[role=tab]").Count);
        Assert.Contains("All tabs (8)", cut.Markup);
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

