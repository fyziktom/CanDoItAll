using CanDoItAll.Web.Composition;

namespace CanDoItAll.Tests.Components;

public sealed class CrmHrNavigationTests
{
    [Fact]
    public void Shell_navigation_contains_crm_hr_entry()
    {
        var item = Assert.Single(ShellNavigation.Items, candidate => candidate.Route == "/crm-hr");

        Assert.Equal("CRM / HR", item.Title);
        Assert.Equal("CH", item.Icon);
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
}
