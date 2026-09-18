using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Bunit;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentGovernanceContextRevisionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Same_target_changed_agent_observation_is_published(bool metadataOnly) {
        await using var f = new GovernancePanelFixture();
        var observed = new List<AgentDefinition?>();
        var cut = f.Context.Render<AgentGovernancePanel>(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.A)
            .Add(c => c.SelectedAgentChanged, (AgentDefinition? agent) => observed.Add(agent)));
        var original = Assert.Single(observed)!;
        var updated = metadataOnly ? original with { Summary = "Updated summary", Tags = ["new tag"] } : original with { Name = "Renamed agent" };
        f.Reads.Catalog = _ => Task.FromResult<IReadOnlyList<AgentDefinition>>([updated]);
        await f.Button(cut, "Refresh").ClickAsync();
        Assert.Equal(2, observed.Count);
        Assert.Equal(updated.Name, observed[^1]!.Name);
        Assert.Equal(updated.Summary, observed[^1]!.Summary);
        Assert.Equal(updated.Tags, observed[^1]!.Tags);
    }

    [Fact]
    public async Task Disappeared_agent_reappears_after_failed_refresh_without_target_change() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        f.Reads.Catalog = _ => Task.FromResult<IReadOnlyList<AgentDefinition>>([]);
        await f.Button(cut, "Refresh").ClickAsync();
        Assert.Equal(AgentChatContextAccessState.Failed, f.Access.Last());
        Assert.Single(f.Selected);
        Assert.DoesNotContain(f.Selected, id => id is null);
        f.Reads.Catalog = _ => Task.FromException<IReadOnlyList<AgentDefinition>>(new IOException("private failure"));
        await f.Button(cut, "Refresh").ClickAsync();
        Assert.Single(f.Selected);
        Assert.Equal(AgentChatContextAccessState.Failed, f.Access.Last());
        f.Reads.Catalog = _ => Task.FromResult(GovernancePanelFixture.Agents);
        await f.Button(cut, "Refresh").ClickAsync();
        Assert.Equal(new Guid?[] { GovernancePanelFixture.A, GovernancePanelFixture.A }, f.Selected);
        Assert.Equal(AgentChatContextAccessState.Ready, f.Access.Last());
    }

    [Fact]
    public async Task Identical_catalog_observation_does_not_publish_again() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        f.Reads.Catalog = _ => Task.FromResult<IReadOnlyList<AgentDefinition>>(GovernancePanelFixture.Agents.Select(a => a with {
            Tags = a.Tags.ToList(), Capabilities = a.Capabilities.ToList(),
            Permissions = a.Permissions with { AllowedSecrets = a.Permissions.NormalizedAllowedSecrets.ToList() }
        }).ToList());
        await f.Button(cut, "Refresh").ClickAsync();
        await f.Button(cut, "Refresh").ClickAsync();
        Assert.Single(f.Selected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_catalog_refresh_does_not_publish_fresh_observation(bool canceled) {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        f.Reads.Catalog = _ => canceled
            ? Task.FromCanceled<IReadOnlyList<AgentDefinition>>(new CancellationToken(true))
            : Task.FromException<IReadOnlyList<AgentDefinition>>(new IOException("private failure"));
        await f.Button(cut, "Refresh").ClickAsync();
        Assert.Single(f.Selected);
        Assert.DoesNotContain("private failure", cut.Markup);
    }

    [Fact]
    public async Task Late_catalog_refresh_cannot_replace_a_newer_observation() {
        await using var f = new GovernancePanelFixture();
        var observed = new List<AgentDefinition?>();
        var cut = f.Context.Render<AgentGovernancePanel>(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.A)
            .Add(c => c.SelectedAgentChanged, (AgentDefinition? agent) => observed.Add(agent)));
        var old = f.Pending<IReadOnlyList<AgentDefinition>>([GovernancePanelFixture.Agents[0] with { Name = "Old result" }]);
        f.Reads.Catalog = old.Read;
        var refresh = f.Button(cut, "Refresh").ClickAsync();
        cut.WaitForAssertion(() => Assert.True(old.Started));
        f.Reads.Catalog = _ => Task.FromResult<IReadOnlyList<AgentDefinition>>([GovernancePanelFixture.Agents[0] with { Name = "Current result" }]);
        await cut.InvokeAsync(() => cut.FindComponent<CanDoItAll.AgentFramework.UI.Governance.AgentGovernanceSurface>().Instance.OnIntent.InvokeAsync(new CanDoItAll.AgentFramework.UI.Governance.GovernanceIntent.Refresh()));
        await f.CompleteAsync(old.Complete);
        await refresh;
        Assert.Equal(new[] { "Agent A", "Current result" }, observed.Select(a => a!.Name));
    }

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
