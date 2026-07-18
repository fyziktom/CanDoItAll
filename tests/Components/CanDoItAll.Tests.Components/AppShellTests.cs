using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AppShellTests : TestContext
{
    public AppShellTests()
    {
        Services.AddSingleton<TooltipService>();
    }

    [Fact]
    public void Renders_current_navigation_without_removed_module_items()
    {
        var cut = RenderComponent<AppShell>(parameters => parameters
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

        var cut = RenderComponent<AppShell>(parameters => parameters
            .Add(component => component.DefaultNavigationMode, AppShellNavigationMode.Expanded)
            .Add(component => component.NavigationItems, ShellNavigation.Items)
            .Add(component => component.Workspaces, workspaces)
            .Add(component => component.OpenedProjects, openedProjects)
            .Add(component => component.CurrentRoute, "scheduler"));

        var desktopSidebarMarkup = cut.Find("aside").InnerHtml;

        Assert.DoesNotContain("cda-shell-pick-button", desktopSidebarMarkup);
        Assert.DoesNotContain("Operational summary, provider health, and recent work.", desktopSidebarMarkup);
        Assert.DoesNotContain("project-structure / /projects/one/structure", desktopSidebarMarkup);
        Assert.Contains("cda-shell-nav-label-row", desktopSidebarMarkup);
        Assert.DoesNotContain("cda-shell-opened-button", desktopSidebarMarkup);
        Assert.Contains("shell-nav-opened-work", desktopSidebarMarkup);
        Assert.Contains("shell-opened-work-panel", desktopSidebarMarkup);
        Assert.Contains("shell-opened-work-project-1", desktopSidebarMarkup);
    }

    [Fact]
    public void Standard_navigation_tooltips_use_delayed_menu_timing()
    {
        var cut = RenderComponent<AppShell>(parameters => parameters
            .Add(component => component.NavigationItems, ShellNavigation.Items)
            .Add(component => component.CurrentRoute, "agents"));

        var dashboardTooltip = Assert.Single(
            cut.FindComponents<TooltipTarget>(),
            component => string.Equals(component.Instance.TestId, "shell-nav-tooltip-dashboard", StringComparison.Ordinal));

        Assert.Equal(TimeSpan.FromSeconds(2), dashboardTooltip.Instance.Delay);
    }

    [Fact]
    public void Brand_icon_uses_default_for_missing_values_and_allows_an_override()
    {
        var defaultCut = RenderComponent<AppShell>();
        var blankCut = RenderComponent<AppShell>(parameters => parameters
            .Add(component => component.BrandIconName, " "));
        var customCut = RenderComponent<AppShell>(parameters => parameters
            .Add(component => component.BrandIconName, "rocket_launch"));

        Assert.Equal("apps", defaultCut.Find(".cda-shell-brand-mark").TextContent.Trim());
        Assert.Equal("apps", blankCut.Find(".cda-shell-brand-mark").TextContent.Trim());
        Assert.Equal("rocket_launch", customCut.Find(".cda-shell-brand-mark").TextContent.Trim());
    }
}
