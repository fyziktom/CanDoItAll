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
        var cut = Render<AppShell>(parameters => parameters
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
        var defaultCut = Render<AppShell>();
        var blankCut = Render<AppShell>(parameters => parameters
            .Add(component => component.BrandIconName, " "));
        var customCut = Render<AppShell>(parameters => parameters
            .Add(component => component.BrandIconName, "rocket_launch"));

        Assert.Equal("apps", defaultCut.Find(".cda-shell-brand-mark").TextContent.Trim());
        Assert.Equal("apps", blankCut.Find(".cda-shell-brand-mark").TextContent.Trim());
        Assert.Equal("rocket_launch", customCut.Find(".cda-shell-brand-mark").TextContent.Trim());
    }

    [Fact]
    public void Standard_page_is_viewport_bounded_and_body_surface_fills_the_available_shell_height()
    {
        var cut = Render<AppShell>(parameters => parameters
            .Add(component => component.Mode, AppShellMode.StandardPage)
            .Add(component => component.ShowRightRail, false)
            .Add(component => component.Body, (RenderFragment)(builder =>
                builder.AddMarkupContent(0, "<div>Standard page content</div>"))));

        var root = cut.Find(".cda-shell-root");
        var frame = cut.Find(".cda-shell-frame");
        var surface = cut.Find(".cda-shell-body-surface");
        var bodyRegion = surface.ParentElement;

        Assert.NotNull(bodyRegion);
        Assert.Equal(
            "height:100vh;height:100dvh;min-height:0;overflow:hidden;",
            root.GetAttribute("style"));
        Assert.Equal(
            "height:100%;min-height:0;overflow:hidden;",
            frame.GetAttribute("style"));
        Assert.Contains("cda-shell-body-surface--standard", surface.ClassList);
        Assert.Contains("flex", bodyRegion.ClassList);
        Assert.Contains("min-h-0", bodyRegion.ClassList);
        Assert.Contains("flex-1", bodyRegion.ClassList);
        Assert.Contains("flex-col", bodyRegion.ClassList);
    }
}
