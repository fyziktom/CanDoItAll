using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentCatalogOperationsIntegrationTests {
    [Fact]
    public async Task Real_operations_respect_initial_data_and_repair_then_reload() {
        var repair = new RecordingRepair();
        await using var host = await AgentUiAdapterTestHost.CreateAsync(services =>
            services.AddSingleton<IAgentFrameworkOrganizationCatalogRepairService>(repair));
        await using var scope = host.App.Services.CreateAsyncScope();
        var operations = Assert.IsType<AgentCatalogOperations>(scope.ServiceProvider.GetRequiredService<IAgentCatalogOperations>());
        var empty = await operations.LoadAsync(new(Repair: true, Agents: [], Providers: [], Teams: []));
        Assert.Equal(1, repair.Reads);
        Assert.Empty(empty.Agents);
        Assert.Empty(empty.Teams);
        Assert.Empty(empty.PrivateProviderById);
        var loaded = await operations.LoadAsync(new(Repair: false));
        Assert.Equal(1, repair.Reads);
        Assert.NotEmpty(loaded.Agents);
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        Assert.Equal((await workspace.ListAgentsAsync(false)).Select(agent => agent.Id), loaded.Agents.Select(agent => agent.Id));
    }

    [Fact]
    public async Task Real_operations_update_and_delete_only_the_selected_team() {
        await using var host = await AgentUiAdapterTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(false)).First();
        var target = await workspace.SaveAgentTeamAsync(new AgentTeamEditorModel { Name = "Catalog seam target" });
        var other = await workspace.SaveAgentTeamAsync(new AgentTeamEditorModel { Name = "Catalog seam survivor" });
        var operations = scope.ServiceProvider.GetRequiredService<IAgentCatalogOperations>();
        await operations.UpdateMembersAsync(target, [agent.Id]);
        Assert.Equal([agent.Id], (await workspace.ListAgentTeamsAsync()).Single(team => team.Id == target).AgentIds);
        await operations.DeleteTeamAsync(target);
        var teams = await workspace.ListAgentTeamsAsync();
        Assert.DoesNotContain(teams, team => team.Id == target);
        Assert.Contains(teams, team => team.Id == other);
    }

    private sealed class RecordingRepair : IAgentFrameworkOrganizationCatalogRepairService {
        public int Reads { get; private set; }
        public Task EnsureCurrentOrganizationCatalogAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return Task.CompletedTask;
        }
    }

}
