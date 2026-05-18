using Bunit;
using CanDoItAll.Components;
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
    public void Renders_more_continuation_panel_when_desktop_navigation_overflows()
    {
        var cut = RenderComponent<AppShell>(parameters => parameters
            .Add(component => component.NavigationItems, ShellNavigation.Items)
            .Add(component => component.CurrentRoute, "automation")
            .Add(component => component.TopBar, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Shell status</div>"))));

        Assert.Contains("app-shell-workbar", cut.Markup);
        Assert.Contains("shell-nav-more", cut.Markup);
        Assert.Contains("expand_less", cut.Markup);
        Assert.Contains("shell-nav-overflow-panel", cut.Markup);
        Assert.Contains("shell-nav-overflow-test-lab", cut.Markup);
        Assert.Contains(">Tests<", cut.Markup);
        Assert.Contains("shell-nav-automation", cut.Markup);
    }

    [Fact]
    public void Expanded_navigation_uses_tooltips_instead_of_inline_subtitles()
    {
        var workspaces = new[]
        {
            new ShellWorkspaceItem("delivery", "Delivery Workspace", "Project authoring, structure, calendars, and prompt sessions.", "/"),
            new ShellWorkspaceItem("quality", "Quality Desk", "Validation runs, test plans, and evidence review.", "/validation"),
            new ShellWorkspaceItem("automation", "Automation Ops", "Activity, automation status, and environment settings.", "/automation")
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
            .Add(component => component.CurrentRoute, "automation"));

        var desktopSidebarMarkup = cut.Find("aside").InnerHtml;

        Assert.DoesNotContain("cda-shell-pick-button", desktopSidebarMarkup);
        Assert.DoesNotContain("Operational summary, provider health, and recent work.", desktopSidebarMarkup);
        Assert.DoesNotContain("project-structure / /projects/one/structure", desktopSidebarMarkup);
        Assert.Contains("cda-shell-nav-label-row", desktopSidebarMarkup);
        Assert.Contains("cda-shell-opened-button", desktopSidebarMarkup);
    }
}
