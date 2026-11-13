using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class AppShellTests : BunitContext
{
    public AppShellTests()
    {
        Services.AddSingleton<TooltipService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Navigation_is_collapsed_by_default() {
        var cut = Render<AppShell>();

        Assert.Equal("collapsed", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));
        Assert.Equal("Expand navigation", cut.Find("[data-testid='shell-navigation-toggle']").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Uncontrolled_navigation_keeps_its_initial_default_and_toggles_locally() {
        List<AppShellNavigationMode> changes = [];
        var cut = Render<AppShell>(parameters => parameters
            .Add(component => component.DefaultNavigationMode, AppShellNavigationMode.Expanded)
            .Add(component => component.NavigationModeChanged, changes.Add));

        cut.Render(parameters => parameters.Add(component => component.DefaultNavigationMode, AppShellNavigationMode.Collapsed));
        Assert.Equal("expanded", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));

        await cut.InvokeAsync(() => cut.Find("[data-testid='shell-navigation-toggle']").ClickAsync());
        Assert.Equal("collapsed", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));

        cut.Render(parameters => parameters.Add(component => component.DefaultNavigationMode, AppShellNavigationMode.Expanded));
        Assert.Equal("collapsed", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));

        await cut.InvokeAsync(() => cut.Find("[data-testid='shell-navigation-toggle']").ClickAsync());
        Assert.Equal("expanded", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));
        Assert.Equal([AppShellNavigationMode.Collapsed, AppShellNavigationMode.Expanded], changes);
    }

    [Fact]
    public async Task Controlled_navigation_awaits_the_callback_and_only_changes_with_owner_parameters() {
        var entered = new TaskCompletionSource<AppShellNavigationMode>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cut = Render<AppShell>(parameters => parameters
            .Add(component => component.NavigationMode, AppShellNavigationMode.Collapsed)
            .Add(component => component.NavigationModeChanged, async nextMode => {
                calls++;
                entered.SetResult(nextMode);
                await release.Task;
            }));

        var click = cut.InvokeAsync(() => cut.Find("[data-testid='shell-navigation-toggle']").ClickAsync());
        try {
            Assert.Equal(AppShellNavigationMode.Expanded, await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(click.IsCompleted);
            Assert.Equal("collapsed", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));
        } finally {
            release.TrySetResult();
            await click;
        }

        Assert.Equal("collapsed", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));
        cut.Render(parameters => parameters.Add(component => component.NavigationMode, AppShellNavigationMode.Expanded));
        Assert.Equal("expanded", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));
        Assert.Equal("Collapse navigation", cut.Find("[data-testid='shell-navigation-toggle']").GetAttribute("aria-label"));

        cut.Render(parameters => parameters.Add(component => component.NavigationMode, AppShellNavigationMode.Collapsed));
        Assert.Equal("collapsed", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Failed_controlled_navigation_change_preserves_the_owner_state() {
        var failure = new IOException("Preference write failed.");
        var cut = Render<AppShell>(parameters => parameters
            .Add(component => component.NavigationMode, AppShellNavigationMode.Expanded)
            .Add(component => component.NavigationModeChanged, _ => Task.FromException(failure)));

        var thrown = await Assert.ThrowsAsync<IOException>(() =>
            cut.InvokeAsync(() => cut.Find("[data-testid='shell-navigation-toggle']").ClickAsync()));

        Assert.Same(failure, thrown);
        Assert.Equal("expanded", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));
    }

    [Fact]
    public async Task Busy_navigation_preference_disables_only_its_toggle() {
        var changes = 0;
        string? destination = null;
        var cut = Render<AppShell>(parameters => parameters
            .Add(component => component.NavigationMode, AppShellNavigationMode.Collapsed)
            .Add(component => component.NavigationModeBusy, true)
            .Add(component => component.NavigationModeChanged, _ => changes++)
            .Add(component => component.NavigationItems, ShellNavigation.Items)
            .Add(component => component.Navigate, route => destination = route));

        Assert.True(cut.Find("[data-testid='shell-navigation-toggle']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='shell-navigation-toggle']").ClickAsync());
        Assert.Equal(0, changes);

        Assert.False(cut.Find("[data-testid='shell-nav-dashboard']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='shell-nav-dashboard']").ClickAsync());
        Assert.Equal("/", destination);

        cut.Render(parameters => parameters.Add(component => component.NavigationModeBusy, false));
        Assert.False(cut.Find("[data-testid='shell-navigation-toggle']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='shell-navigation-toggle']").ClickAsync());
        Assert.Equal(1, changes);
        Assert.Equal("collapsed", cut.Find("[data-testid='app-shell-sidebar']").GetAttribute("data-navigation-mode"));
    }

    [Fact]
    public void Renders_current_navigation_without_removed_module_items()
    {
        var cut = Render<AppShell>(parameters => parameters
            .Add(component => component.NavigationItems, ShellNavigation.Items)
            .Add(component => component.CurrentRoute, "scheduler")
            .Add(component => component.TopBar, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Shell status</div>"))));

        Assert.Contains("app-shell-workbar", cut.Markup);
        Assert.Contains("shell-nav-test-lab", cut.Markup);
        Assert.Contains("shell-nav-scheduler", cut.Markup);
        Assert.DoesNotContain("shell-nav-automation", cut.Markup);
        Assert.DoesNotContain("shell-nav-activity", cut.Markup);
        Assert.DoesNotContain("shell-nav-validation", cut.Markup);
    }

    [Fact]
    public void Expanded_navigation_uses_tooltips_instead_of_inline_subtitles()
    {
        var workspaces = new[]
        {
            new ShellWorkspaceItem("delivery", "Delivery Workspace", "Project authoring, structure, calendars, and prompt sessions.", "/"),
            new ShellWorkspaceItem("quality", "Quality Desk", "Test plans and evidence review.", "/test-lab"),
            new ShellWorkspaceItem("operations", "Operations", "Scheduler, runtime settings, and environment status.", "/scheduler")
        };
        var openedProjects = new[]
        {
            new WorkbenchTabState("project-1", "EBilling - SAP - Structure", "/projects/one/structure", TabKind: "project-structure")
        };

        var cut = Render<AppShell>(parameters => parameters
            .Add(component => component.DefaultNavigationMode, AppShellNavigationMode.Expanded)
            .Add(component => component.NavigationItems, ShellNavigation.Items)
            .Add(component => component.Workspaces, workspaces)
            .Add(component => component.OpenedProjects, openedProjects)
            .Add(component => component.CurrentRoute, "scheduler"));

        var desktopSidebarMarkup = cut.Find("aside").InnerHtml;

        Assert.DoesNotContain("app-shell-pick-button", desktopSidebarMarkup);
        Assert.DoesNotContain("Operational summary, provider health, and recent work.", desktopSidebarMarkup);
        Assert.DoesNotContain("project-structure / /projects/one/structure", desktopSidebarMarkup);
        Assert.Contains("app-shell-nav-label-row", desktopSidebarMarkup);
        Assert.DoesNotContain("app-shell-opened-button", desktopSidebarMarkup);
        Assert.Contains("shell-nav-opened-work", desktopSidebarMarkup);
        Assert.Contains("shell-opened-work-panel", desktopSidebarMarkup);
        Assert.Contains("shell-opened-work-project-1", desktopSidebarMarkup);
    }

    [Fact]
    public void Standard_navigation_tooltips_use_delayed_menu_timing()
    {
        var cut = Render<AppShell>(parameters => parameters
            .Add(component => component.NavigationItems, ShellNavigation.Items)
            .Add(component => component.CurrentRoute, "agents"));

        var dashboardTooltip = Assert.Single(
            cut.FindComponents<TooltipTarget>(),
            component => string.Equals(component.Instance.TestId, "shell-nav-tooltip-dashboard", StringComparison.Ordinal));

        Assert.Equal(TimeSpan.FromSeconds(2), dashboardTooltip.Instance.Delay);
    }

    [Fact]
    public void Modern_shell_omits_the_retired_sidebar_brand_mark()
    {
        var defaultCut = Render<AppShell>();
        var blankCut = Render<AppShell>(parameters => parameters
            .Add(component => component.BrandIconName, " "));
        var customCut = Render<AppShell>(parameters => parameters
            .Add(component => component.BrandIconName, "rocket_launch"));

        Assert.Empty(defaultCut.FindAll(".app-shell-brand-mark"));
        Assert.Empty(blankCut.FindAll(".app-shell-brand-mark"));
        Assert.Empty(customCut.FindAll(".app-shell-brand-mark"));
    }

    [Fact]
    public void Standard_page_is_viewport_bounded_and_body_surface_fills_the_available_shell_height()
    {
        var cut = Render<AppShell>(parameters => parameters
            .Add(component => component.Mode, AppShellMode.StandardPage)
            .Add(component => component.ShowRightRail, false)
            .Add(component => component.Body, (RenderFragment)(builder =>
                builder.AddMarkupContent(0, "<div>Standard page content</div>"))));

        var root = cut.Find(".app-shell-root");
        var frame = cut.Find(".app-shell-frame");
        var surface = cut.Find(".app-shell-body-surface");
        var bodyRegion = surface.ParentElement;

        Assert.NotNull(bodyRegion);
        Assert.Equal(
            "height:100vh;height:100dvh;min-height:0;overflow:clip;",
            root.GetAttribute("style"));
        Assert.Equal(
            "height:100%;min-height:0;overflow:clip;",
            frame.GetAttribute("style"));
        Assert.Contains("app-shell-body-surface--standard", surface.ClassList);
        Assert.Contains("flex", bodyRegion.ClassList);
        Assert.Contains("min-h-0", bodyRegion.ClassList);
        Assert.Contains("flex-1", bodyRegion.ClassList);
        Assert.Contains("flex-col", bodyRegion.ClassList);
    }
}
