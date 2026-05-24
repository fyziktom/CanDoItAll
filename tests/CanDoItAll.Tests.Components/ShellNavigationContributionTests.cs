using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Web.Composition;

namespace CanDoItAll.Tests.Components;

public sealed class ShellNavigationContributionTests
{
    [Fact]
    public void AgentFramework_contribution_inserts_workflows_after_agents()
    {
        var items = ShellNavigation.GetItems(0, [new AgentFrameworkShellNavigationContributor()]);
        var agentsIndex = items.ToList().FindIndex(item => item.Route == "/agents");
        var workflowsIndex = items.ToList().FindIndex(item => item.Route == "/agents/workflows");
        var resourcesIndex = items.ToList().FindIndex(item => item.Route == "/resources");

        Assert.True(agentsIndex >= 0);
        Assert.Equal("Agents", items[agentsIndex].Title);
        Assert.Equal(agentsIndex + 1, workflowsIndex);
        Assert.True(resourcesIndex > workflowsIndex);
    }

    [Fact]
    public void Contributed_route_matching_prefers_workflows_over_agents_parent()
    {
        var item = ShellNavigation.MatchRoute("agents/workflows", [new AgentFrameworkShellNavigationContributor()]);

        Assert.Equal("/agents/workflows", item.Route);
        Assert.Equal("Workflows", item.Title);
    }

    [Fact]
    public void AgentFramework_contribution_marks_workflows_as_subitem_for_future_menu_design()
    {
        var contribution = Assert.Single(new AgentFrameworkShellNavigationContributor().GetShellNavigationContributions());

        Assert.Equal("agent-framework", contribution.ModuleId);
        Assert.Equal("/agents", contribution.ParentRoute);
        Assert.True(contribution.IsSubItem);
        Assert.Contains("flat main menu", contribution.DesignNote);
    }
}
