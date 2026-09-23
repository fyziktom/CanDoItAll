using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentsWorkspaceStateTests {
    [Theory]
    [InlineData(AgentWorkspaceSection.Overview, AgentWorkspaceTabs.Overview)]
    [InlineData(AgentWorkspaceSection.Agents, AgentWorkspaceTabs.Agents)]
    [InlineData(AgentWorkspaceSection.SimpleChats, AgentWorkspaceTabs.SimpleChats)]
    [InlineData(AgentWorkspaceSection.Providers, AgentWorkspaceTabs.Providers)]
    [InlineData(AgentWorkspaceSection.RequestHistory, AgentWorkspaceTabs.RequestHistory)]
    [InlineData(AgentWorkspaceSection.Voice, AgentWorkspaceTabs.Voice)]
    [InlineData(AgentWorkspaceSection.FloatingChat, AgentWorkspaceTabs.FloatingChat)]
    [InlineData(AgentWorkspaceSection.Chat, AgentWorkspaceTabs.Chat)]
    [InlineData(AgentWorkspaceSection.Capabilities, AgentWorkspaceTabs.Capabilities)]
    [InlineData(AgentWorkspaceSection.Governance, AgentWorkspaceTabs.Governance)]
    [InlineData(AgentWorkspaceSection.Diagnostics, AgentWorkspaceTabs.Diagnostics)]
    public void Sections_preserve_existing_tab_keys(AgentWorkspaceSection section, string key) {
        Assert.Equal(key, section.ToTabKey());
        Assert.Equal(section, AgentWorkspaceSections.FromTabKey(key));
        var state = new AgentsWorkspaceState().ApplyRoute(AgentWorkspaceRouteState.Parse(key, null, null, null, null, null, null));
        Assert.Equal(section, state.Section);
        Assert.Equal(section == AgentWorkspaceSection.Overview ? "/agents" : $"/agents?tab={key}",
            AgentWorkspaceRouteState.Build(state.ToRoute()));
    }

    [Fact]
    public void Leaving_catalog_clears_team_but_retains_agent() {
        var state = new AgentsWorkspaceState {
            Section = AgentWorkspaceSection.Agents,
            AgentId = Guid.NewGuid(),
            TeamId = Guid.NewGuid(),
            SelectionAccess = AgentChatContextAccessState.Ready
        };
        var history = state.SelectSection(AgentWorkspaceSection.RequestHistory);
        Assert.Null(history.TeamId);
        Assert.Equal(state.AgentId, history.AgentId);
        Assert.Equal(AgentChatContextAccessState.Ready, history.SelectionAccess);
        Assert.Equal(AgentChatContextAccessState.Loading, history.SelectSection(AgentWorkspaceSection.Chat).SelectionAccess);
        Assert.NotNull(state.TeamId);
    }

    [Fact]
    public void Same_route_keeps_access_readiness_but_changed_selection_requires_loading() {
        var state = new AgentsWorkspaceState {
            Section = AgentWorkspaceSection.Agents,
            AgentId = Guid.NewGuid(),
            SelectionAccess = AgentChatContextAccessState.Ready
        };
        Assert.Equal(state, state.ApplyRoute(state.ToRoute()));
        var changed = state.ApplyRoute(state.ToRoute() with { AgentId = Guid.NewGuid() });
        Assert.Equal(AgentChatContextAccessState.Loading, changed.SelectionAccess);
        Assert.NotEqual(state.AgentId, changed.AgentId);
    }
}
