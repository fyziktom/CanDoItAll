using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Bunit;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentGovernanceContextRevisionTests {
    [Fact]
    public async Task Accepted_new_route_target_resolves_parent_loading_access() {
        await using var fixture = new GovernancePanelFixture();
        var workspace = new AgentsWorkspaceState().ApplyRoute(AgentWorkspaceRouteState.Parse(
            AgentWorkspaceTabs.Governance, GovernancePanelFixture.A, null, null, null, null, null));
        var cut = fixture.Context.Render<AgentGovernancePanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, GovernancePanelFixture.A)
            .Add(component => component.SelectedAgentChanged, (AgentDefinition? agent) => workspace = workspace with { AgentId = agent?.Id })
            .Add(component => component.ContextAccessStateChanged, (AgentChatContextAccessState access) => workspace = workspace with { SelectionAccess = access }));
        Assert.Equal(AgentChatContextAccessState.Ready, workspace.SelectionAccess);
        workspace = workspace.ApplyRoute(AgentWorkspaceRouteState.Parse(
            AgentWorkspaceTabs.Governance, GovernancePanelFixture.B, null, null, null, null, null));
        Assert.Equal(AgentChatContextAccessState.Loading, workspace.SelectionAccess);
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, GovernancePanelFixture.B));
        Assert.Equal(GovernancePanelFixture.B, workspace.AgentId);
        Assert.Equal(AgentChatContextAccessState.Ready, workspace.SelectionAccess);
    }

    [Fact]
    public async Task Returning_to_valid_agent_after_missing_request_republishes_accepted_context() {
        await using var fixture = new GovernancePanelFixture();
        var cut = fixture.Render(GovernancePanelFixture.A);
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, Guid.Empty));
        Assert.Equal(AgentChatContextAccessState.Failed, fixture.Access.Last());
        Assert.Single(fixture.Selected);
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, GovernancePanelFixture.A));
        Assert.Equal(AgentChatContextAccessState.Ready, fixture.Access.Last());
        Assert.Equal(new Guid?[] { GovernancePanelFixture.A, GovernancePanelFixture.A }, fixture.Selected);
    }
}
