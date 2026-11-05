using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.UI.Catalog;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCatalogOpeningSnapshotTests {
    [Fact]
    public async Task Catalog_opening_reads_current_data_after_its_observation_instead_of_using_an_earlier_parent_snapshot() {
        var id = Guid.NewGuid();
        var old = AgentCatalogPanelTests.CreateAgent(id, "Earlier parent snapshot", "");
        var current = old with { Name = "Current native catalog", UpdatedAtUtc = old.UpdatedAtUtc.AddSeconds(1) };
        var operations = new CurrentCatalogOperations(new([current], [], new Dictionary<Guid, bool>()));
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IAgentCatalogOperations>(operations));
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters.Add(component => component.InitialAgents, new[] { old }));
        cut.WaitForAssertion(() => Assert.Contains("Current native catalog", cut.Markup));
        Assert.DoesNotContain("Earlier parent snapshot", cut.Markup);
    }

    [Fact]
    public async Task Membership_opening_reads_its_original_after_subscribing_even_if_the_selected_catalog_row_is_old() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var first = await workspace.SaveAgentDefinitionAsync(new() { Name = "Membership first fixture" });
        var second = await workspace.SaveAgentDefinitionAsync(new() { Name = "Membership peer fixture" });
        var id = await workspace.SaveAgentTeamAsync(new() { Name = "Membership initial snapshot", AgentIds = [first.Id] });
        var original = (await workspace.ListAgentTeamsAsync()).Single(team => team.Id == id);
        await workspace.UpdateAgentTeamMembersAsync(id, [first.Id, second.Id], original.UpdatedAtUtc);
        var cut = harness.Context.Render<AgentTeamMembersDialog>(parameters => parameters.Add(component => component.Team, original)
            .Add(component => component.Agents, new[] { first, second }));
        cut.WaitForAssertion(() => {
            var peer = cut.FindComponents<AgentSelectionCard>().Single(component => component.Instance.Agent.Id == second.Id);
            Assert.Contains("In team", peer.Markup);
        });
    }

    private sealed class CurrentCatalogOperations(AgentCatalogSnapshot current) : IAgentCatalogOperations {
        public Task<AgentCatalogSnapshot> LoadAsync(AgentCatalogLoadRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(current with { Agents = request.Agents ?? current.Agents, Teams = request.Teams ?? current.Teams });
        public Task DeleteTeamAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateMembersAsync(Guid id, IReadOnlyList<Guid> agents, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
