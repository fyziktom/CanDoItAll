using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Memory;
using CanDoItAll.Modules.Processes;
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
    public void Process_contribution_inserts_live_processes_after_contributed_process_parent()
    {
        var items = ShellNavigation.GetItems(
            0,
            [
                new AgentFrameworkShellNavigationContributor(),
                new ProcessesShellNavigationContributor()
            ]);
        var processesIndex = items.ToList().FindIndex(item => item.Route == "/processes");
        var liveProcessesIndex = items.ToList().FindIndex(item => item.Route == "/processes/live");
        var resourcesIndex = items.ToList().FindIndex(item => item.Route == "/resources");

        Assert.True(processesIndex >= 0);
        Assert.Equal("Processes", items[processesIndex].Title);
        Assert.Equal(processesIndex + 1, liveProcessesIndex);
        Assert.True(resourcesIndex > liveProcessesIndex);
        Assert.DoesNotContain(items, item => item.Route == "/cognitive-memory");
    }

    [Fact]
    public void Memory_contribution_reuses_cognitive_memory_slot_after_live_processes()
    {
        var items = ShellNavigation.GetItems(
            0,
            [
                new AgentFrameworkShellNavigationContributor(),
                new ProcessesShellNavigationContributor(),
                new MemoryShellNavigationContributor()
            ]);
        var liveProcessesIndex = items.ToList().FindIndex(item => item.Route == "/processes/live");
        var memoryIndex = items.ToList().FindIndex(item => item.Route == "/memory");

        Assert.True(liveProcessesIndex > 0);
        Assert.Equal(liveProcessesIndex + 1, memoryIndex);
        Assert.Equal("Memory Providers", items[memoryIndex].Title);
        Assert.Equal("psychology", items[memoryIndex].Icon);
        Assert.DoesNotContain(items, item => item.Route == "/cognitive-memory");
    }

    [Fact]
    public void Contributed_route_matching_prefers_live_processes_over_processes_parent()
    {
        var item = ShellNavigation.MatchRoute(
            "processes/live",
            [new ProcessesShellNavigationContributor()]);

        Assert.Equal("/processes/live", item.Route);
        Assert.Equal("Live Processes", item.Title);
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
