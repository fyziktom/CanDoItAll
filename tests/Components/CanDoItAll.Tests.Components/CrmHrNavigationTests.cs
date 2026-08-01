using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Web.Composition;
using WebMainLayout = CanDoItAll.Web.Components.Layout.MainLayout;

namespace CanDoItAll.Tests.Components;

public sealed class CrmHrNavigationTests
{
    [Fact]
    public void Shell_navigation_contains_crm_hr_entry()
    {
        var item = Assert.Single(ShellNavigation.Items, candidate => candidate.Route == "/crm-hr");

        Assert.Equal("CRM / HR", item.Title);
        Assert.Equal("groups", item.Icon);
        Assert.DoesNotContain(
            ShellNavigation.Items,
            candidate => candidate.Route.StartsWith("/crm-hr/", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("crm-hr", "/crm-hr")]
    [InlineData("crm-hr/directory", "/crm-hr")]
    [InlineData("crm-hr/agents", "/crm-hr")]
    public void MatchRoute_maps_crm_hr_child_routes_to_the_module_navigation_item(string route, string expectedRoute)
    {
        var item = ShellNavigation.MatchRoute(route);

        Assert.Equal(expectedRoute, item.Route);
    }

    [Theory]
    [InlineData(CrmHrWorkspaceArea.Home, "/crm-hr", "CRM / HR")]
    [InlineData(CrmHrWorkspaceArea.Directory, "/crm-hr/directory", "CRM Directory")]
    [InlineData(CrmHrWorkspaceArea.Crm, "/crm-hr/crm", "CRM")]
    [InlineData(CrmHrWorkspaceArea.Workforce, "/crm-hr/workforce", "CRM Workforce")]
    [InlineData(CrmHrWorkspaceArea.Recruiting, "/crm-hr/recruiting", "CRM Recruiting")]
    [InlineData(CrmHrWorkspaceArea.Agents, "/crm-hr/agents", "CRM Agents")]
    [InlineData(CrmHrWorkspaceArea.Assignments, "/crm-hr/assignments", "CRM Assignments")]
    public void Route_catalog_defines_contextual_workbench_titles(
        CrmHrWorkspaceArea area,
        string route,
        string expectedTitle)
    {
        var definition = CrmHrRouteCatalog.Get(area);

        Assert.Equal(route, definition.Route);
        Assert.Equal(expectedTitle, definition.WorkbenchTitle);
        Assert.True(CrmHrRouteCatalog.TryResolve(route, out var resolved));
        Assert.Equal(definition, resolved);
    }

    [Fact]
    public void Route_catalog_keeps_routes_keys_and_titles_distinct()
    {
        Assert.Equal(7, CrmHrRouteCatalog.Items.Count);
        Assert.Equal(
            CrmHrRouteCatalog.Items.Count,
            CrmHrRouteCatalog.Items.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            CrmHrRouteCatalog.Items.Count,
            CrmHrRouteCatalog.Items.Select(item => item.Route).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            CrmHrRouteCatalog.Items.Count,
            CrmHrRouteCatalog.Items.Select(item => item.WorkbenchTitle).Distinct(StringComparer.Ordinal).Count());
        Assert.False(CrmHrRouteCatalog.TryResolve("/projects", out _));
        Assert.False(CrmHrRouteCatalog.TryResolve("/crm-hr-administration", out _));
    }

    [Fact]
    public void Main_layout_builds_crm_hr_tabs_with_contextual_titles_and_stable_ids()
    {
        var layout = new WebMainLayout();

        foreach (var definition in CrmHrRouteCatalog.Items)
        {
            var descriptor = layout.BuildPageDescriptor(definition.Route, definition.Route);

            Assert.Equal(definition.WorkbenchTitle, descriptor.Title);
            Assert.Equal($"route:{definition.Route.Trim('/').Replace('/', ':')}", descriptor.TabId);
            Assert.Equal("CRM / HR", descriptor.TabGroup);
        }
    }
}
