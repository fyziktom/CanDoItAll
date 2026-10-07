using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class TeamMetadataOwnershipTests {
    [Fact]
    public async Task Metadata_preserves_membership_changed_by_another_owner() {
        var store = CreateStore(out var held);
        var first = Owner(store);
        var second = Owner(store);
        var original = Assert.Single(held.Catalog.AgentTeams);
        var editor = AgentTeamEditorModel.FromDefinition(original);
        editor.Description = "Metadata edit";
        await second.UpdateAgentTeamMembersAsync(original.Id, [held.Catalog.Agents[1].Id]);
        await SaveMetadataAsync(first, editor);
        var saved = Assert.Single(held.Catalog.AgentTeams);
        Assert.Equal([held.Catalog.Agents[1].Id], saved.AgentIds);
        Assert.Equal("Metadata edit", saved.Description);
    }

    [Fact]
    public async Task Metadata_submission_is_frozen_before_catalog_coordination() {
        var store = CreateStore(out var held);
        var owner = Owner(store);
        var editor = AgentTeamEditorModel.FromDefinition(Assert.Single(held.Catalog.AgentTeams));
        editor.Description = "Original description";
        editor.Icon = "engineering";
        held.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = SaveMetadataAsync(owner, editor);
        Assert.Equal(1, held.Updates);
        editor.Description = "Later edit";
        editor.Icon = "hub";
        editor.AgentIds.Clear();
        held.Hold.SetResult();
        await save;
        var saved = Assert.Single(held.Catalog.AgentTeams);
        Assert.Equal("Original description", saved.Description);
        Assert.Equal("engineering", saved.Icon);
        Assert.Single(saved.AgentIds);
    }

    [Fact]
    public async Task Metadata_update_does_not_recreate_a_deleted_target() {
        var store = CreateStore(out var held);
        var owner = Owner(store);
        var editor = AgentTeamEditorModel.FromDefinition(Assert.Single(held.Catalog.AgentTeams));
        await owner.DeleteAgentTeamAsync(editor.Id!.Value);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => SaveMetadataAsync(owner, editor));
        Assert.Empty(held.Catalog.AgentTeams);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" original ")]
    public async Task Invalid_or_duplicate_name_is_rejected_without_mutation(string name) {
        var store = CreateStore(out var held);
        var original = held.Catalog;
        await Assert.ThrowsAsync<AgentTeamMetadataRejectedException>(() => Owner(store).SaveAgentTeamMetadataAsync(new() { Name = name }));
        Assert.Same(original, held.Catalog);
    }

    [Fact]
    public async Task Member_ids_are_frozen_before_coordination_and_metadata_is_preserved() {
        var store = CreateStore(out var held);
        var original = Assert.Single(held.Catalog.AgentTeams);
        var ids = new List<Guid> { held.Catalog.Agents[1].Id };
        held.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Owner(store).UpdateAgentTeamMembersAsync(original.Id, ids);
        ids.Clear();
        held.Hold.SetResult();
        var saved = await pending;
        Assert.Equal([held.Catalog.Agents[1].Id], saved.AgentIds);
        Assert.Equal(original.Name, saved.Name);
        Assert.Equal(original.Description, saved.Description);
        Assert.Equal(original.Icon, saved.Icon);
    }

    [Fact]
    public async Task Missing_member_is_refused_without_changes() {
        var store = CreateStore(out var held);
        var original = held.Catalog;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(store).UpdateAgentTeamMembersAsync(original.AgentTeams[0].Id, [Guid.NewGuid()]));
        Assert.Same(original, held.Catalog);
    }

    [Fact]
    public async Task Legacy_upsert_still_creates_supplied_identity_and_owns_its_full_membership() {
        var store = CreateStore(out var held);
        var id = Guid.NewGuid();
        var result = await Owner(store).SaveAgentTeamAsync(new() { Id = id, Name = "Legacy team", AgentIds = [held.Catalog.Agents[1].Id] });
        Assert.Equal(id, result);
        Assert.Equal([held.Catalog.Agents[1].Id], held.Catalog.AgentTeams.Single(team => team.Id == id).AgentIds);
    }

    [Fact]
    public async Task Deleting_group_preserves_agents_and_neighbor_group() {
        var store = CreateStore(out var held);
        var originalAgents = held.Catalog.Agents;
        var originalTeam = Assert.Single(held.Catalog.AgentTeams);
        var neighbor = originalTeam with { Id = Guid.NewGuid(), Name = "Neighbor" };
        held.Catalog = held.Catalog with { AgentTeams = [originalTeam, neighbor] };
        await Owner(store).DeleteAgentTeamAsync(originalTeam.Id);
        Assert.Same(originalAgents, held.Catalog.Agents);
        Assert.Same(neighbor, Assert.Single(held.Catalog.AgentTeams));
    }

    private static async Task<Guid> SaveMetadataAsync(AgentFrameworkWorkspaceCatalogService owner, AgentTeamEditorModel model)
        => (await owner.SaveAgentTeamMetadataAsync(model)).Id!.Value;

    private static AgentFrameworkWorkspaceCatalogService Owner(ISandboxWorkspaceStore store) => new(store, null!, null!, null!, null!, null!, null!);

    private static ISandboxWorkspaceStore CreateStore(out CoordinatedCatalog state) {
        var store = DispatchProxy.Create<ISandboxWorkspaceStore, CoordinatedCatalog>();
        state = (CoordinatedCatalog)(object)store;
        var first = Agent("First");
        var second = Agent("Second");
        state.Catalog = SandboxWorkspaceCatalog.Empty with {
            Agents = [first, second],
            AgentTeams = [new(Guid.NewGuid(), "Original", "", [first.Id], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)]
        };
        return store;
    }

    private static AgentDefinition Agent(string name) => new(Guid.NewGuid(), name, "Fixture", "", "", default, null, "fixture-model",
        AgentWorkloadKind.General, AgentChatHistoryMode.ProviderDefault, 0, false, false, "{}", false, "",
        AgentPermissionsPolicy.Default, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    public class CoordinatedCatalog : DispatchProxy {
        private readonly SemaphoreSlim coordination = new(1);
        public TaskCompletionSource? Hold { get; set; }
        public SandboxWorkspaceCatalog Catalog { get; set; } = SandboxWorkspaceCatalog.Empty;
        public int Updates { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch {
            nameof(ISandboxWorkspaceStore.UpdateCatalogAsync) => UpdateAsync((Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog>)args![0]!),
            nameof(ISandboxWorkspaceStore.LoadCatalogAsync) => Task.FromResult(Catalog),
            _ => throw new InvalidOperationException("Unexpected catalog operation.")
        };
        private async Task<SandboxWorkspaceCatalog> UpdateAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update) {
            Updates++;
            if (Hold is not null) {
                await Hold.Task;
            }
            await coordination.WaitAsync();
            try {
                Catalog = update(Catalog);
                return Catalog;
            } finally {
                coordination.Release();
            }
        }
    }
}
