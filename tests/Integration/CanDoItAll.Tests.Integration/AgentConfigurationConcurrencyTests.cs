using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class AgentConfigurationConcurrencyTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Detached_catalog_read_must_not_borrow_retired_UI_write_authority_but_a_detached_write_remains_denied(bool write) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await CreateAgentAsync(workspace, "Detached catalog original");
        var original = await workspace.GetAgentEditorAsync(id);
        original.Name = "Detached forbidden write";
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var originalScope = new EditingScope(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> detached;
        using (services.GetRequiredService<AgentConfigurationWriteContext>().Enter(originalScope, new(Guid.NewGuid(), Guid.NewGuid()), _ => ValueTask.CompletedTask)) {
            detached = Task.Run(async () => {
                await release.Task;
                if (write) {
                    await workspace.SaveAgentAsync(original);
                    return false;
                }
                return (await workspace.ListAgentsAsync(false)).Any(agent => agent.Id == id);
            });
        }
        release.SetResult();
        if (write) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => detached);
        } else {
            Assert.True(await detached);
        }
        Assert.Equal("Detached catalog original", (await workspace.GetAgentEditorAsync(id)).Name);
    }

    [Fact]
    public async Task Save_returns_the_exact_accepted_configuration_even_after_a_later_peer_commit() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var request = new AgentEditorModel { Name = "  Accepted configuration  ", Instructions = "Accepted instructions" };
        var accepted = await workspace.SaveAgentDefinitionAsync(request);
        var peer = await workspace.GetAgentEditorAsync(accepted.Id);
        peer.Instructions = "Later peer instructions";
        await workspace.SaveAgentAsync(peer);
        var current = await workspace.GetAgentEditorAsync(accepted.Id);
        Assert.Equal("Accepted configuration", accepted.Name);
        Assert.Equal("Accepted instructions", accepted.Instructions);
        Assert.Equal("Later peer instructions", current.Instructions);
        Assert.NotEqual(accepted.UpdatedAtUtc, current.ExpectedUpdatedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_native_publication_refuses_retired_UI_authority_or_a_different_original_dataset(bool otherDataset) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await CreateAgentAsync(workspace, "UI original authority agent");
        var original = await workspace.GetAgentEditorAsync(id);
        original.Name = "Denied UI replacement";
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var originalScope = new EditingScope(EditingScopeKind.Dataset, otherDataset ? Guid.NewGuid() : database.Profile.Profile.Id, database.Generation);
        var calls = 0;
        using (services.GetRequiredService<AgentConfigurationWriteContext>().Enter(originalScope, new(Guid.NewGuid(), Guid.NewGuid()), token => {
            calls++;
            return ValueTask.FromException(new UnauthorizedAccessException("Retired original UI actor."));
        })) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workspace.SaveAgentAsync(original));
        }
        Assert.Equal(otherDataset ? 0 : 1, calls);
        Assert.Equal("UI original authority agent", (await workspace.GetAgentEditorAsync(id)).Name);
    }

    [Fact]
    public async Task Native_committed_configuration_emits_only_scoped_metadata_and_preserves_the_original_editor_identity() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await CreateAgentAsync(workspace, "Observed native agent");
        var original = await workspace.GetAgentEditorAsync(id);
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var topic = new EditingTopic(new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation), EditingOwner.AgentDefinition, id);
        var feed = services.GetRequiredService<EditingChangeFeed>();
        using var subscriber = feed.Subscribe(topic);
        var editor = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var checks = 0;
        using (services.GetRequiredService<AgentConfigurationWriteContext>().Enter(topic.Scope, new(editor, operation), token => {
            token.ThrowIfCancellationRequested();
            checks++;
            return ValueTask.CompletedTask;
        })) {
            original.Summary = "One accepted native configuration change";
            await workspace.SaveAgentAsync(original);
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var change = Assert.Single((await subscriber.ReadAsync(timeout.Token)).Changes);
        Assert.Equal(topic, change.Resource);
        Assert.Equal(editor, change.OriginEditorId);
        Assert.Equal(operation, change.OperationId);
        Assert.True(checks >= 2);
        Assert.Equal(1, feed.DeliveredHints);
        await Assert.ThrowsAsync<AgentCatalogConcurrencyException>(() => workspace.SaveAgentAsync(original));
        Assert.Equal(1, feed.DeliveredHints);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conditional_deletion_refuses_a_changed_original_and_accepts_only_the_reviewed_head(bool agent) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        if (agent) {
            var id = await CreateAgentAsync(workspace, "Conditional delete agent");
            var original = await workspace.GetAgentEditorAsync(id);
            var peer = await workspace.GetAgentEditorAsync(id);
            peer.Summary = "Peer changed before deletion";
            await workspace.SaveAgentAsync(peer);
            await Assert.ThrowsAsync<AgentCatalogConcurrencyException>(() => workspace.DeleteAgentAsync(id, original.ExpectedUpdatedAtUtc!.Value));
            var reviewed = await workspace.GetAgentEditorAsync(id);
            Assert.Equal(peer.Summary, reviewed.Summary);
            await workspace.DeleteAgentAsync(id, reviewed.ExpectedUpdatedAtUtc!.Value);
            Assert.DoesNotContain(await workspace.ListAgentsAsync(), item => item.Id == id);
        } else {
            var id = await workspace.SaveAgentTeamAsync(new() { Name = "Conditional delete team" });
            var original = await workspace.GetAgentTeamEditorAsync(id);
            var peer = await workspace.GetAgentTeamEditorAsync(id);
            peer.Description = "Peer changed before deletion";
            await workspace.SaveAgentTeamMetadataAsync(peer);
            await Assert.ThrowsAsync<AgentTeamConcurrencyException>(() => workspace.DeleteAgentTeamAsync(id, original.ExpectedUpdatedAtUtc!.Value));
            var reviewed = await workspace.GetAgentTeamEditorAsync(id);
            Assert.Equal(peer.Description, reviewed.Description);
            await workspace.DeleteAgentTeamAsync(id, reviewed.ExpectedUpdatedAtUtc!.Value);
            Assert.DoesNotContain(await workspace.ListAgentTeamsAsync(), item => item.Id == id);
        }
    }

    [Fact]
    public async Task Conditional_member_replacement_refuses_an_older_set_without_removing_the_peer_member() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var first = await CreateAgentAsync(workspace, "Original set member");
        var second = await CreateAgentAsync(workspace, "Peer set member");
        var id = await workspace.SaveAgentTeamAsync(new() { Name = "Conditional membership team", AgentIds = [first] });
        var original = await workspace.GetAgentTeamEditorAsync(id);
        var peer = await workspace.UpdateAgentTeamMembersAsync(id, [first, second], original.ExpectedUpdatedAtUtc!.Value);
        await Assert.ThrowsAsync<AgentTeamConcurrencyException>(() => workspace.UpdateAgentTeamMembersAsync(id, [], original.ExpectedUpdatedAtUtc.Value));
        Assert.Equal(2, (await workspace.GetAgentTeamEditorAsync(id)).AgentIds.Count);
        await workspace.UpdateAgentTeamMembersAsync(id, [second], peer.UpdatedAtUtc);
        Assert.Equal([second], (await workspace.GetAgentTeamEditorAsync(id)).AgentIds);
    }

    [Fact]
    public async Task Metadata_only_edit_preserves_members_added_by_another_owner() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var member = await CreateAgentAsync(workspace, "Metadata preserved member");
        var id = await workspace.SaveAgentTeamAsync(new() { Name = "Field owned metadata team" });
        var original = await workspace.GetAgentTeamEditorAsync(id);
        await workspace.UpdateAgentTeamMembersAsync(id, [member], original.ExpectedUpdatedAtUtc!.Value);
        original.Description = "Only my description";
        await workspace.SaveAgentTeamMetadataAsync(original);
        var saved = await workspace.GetAgentTeamEditorAsync(id);
        Assert.Equal(original.Description, saved.Description);
        Assert.Equal([member], saved.AgentIds);
    }

    [Fact]
    public async Task Explicit_member_add_preserves_peer_metadata_and_refuses_a_recreated_team_lifetime() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var first = await CreateAgentAsync(workspace, "Explicit first member");
        var second = await CreateAgentAsync(workspace, "Explicit second member");
        var id = await workspace.SaveAgentTeamAsync(new() { Name = "Explicit member team", AgentIds = [first] });
        var original = (await workspace.ListAgentTeamsAsync()).Single(team => team.Id == id);
        var peer = await workspace.GetAgentTeamEditorAsync(id);
        peer.Name = "Peer renamed member team";
        await workspace.SaveAgentTeamMetadataAsync(peer);
        var added = await workspace.EnsureAgentTeamMemberAsync(id, original.CreatedAtUtc, second);
        Assert.Equal(peer.Name, added.Name);
        Assert.Equal(2, added.AgentIds.Count);
        Assert.Equal(added.UpdatedAtUtc, (await workspace.EnsureAgentTeamMemberAsync(id, original.CreatedAtUtc, second)).UpdatedAtUtc);
        await workspace.DeleteAgentTeamAsync(id, added.UpdatedAtUtc);
        await workspace.SaveAgentTeamAsync(new() { Id = id, Name = "Recreated team" });
        await Assert.ThrowsAsync<AgentTeamConcurrencyException>(() => workspace.EnsureAgentTeamMemberAsync(id, original.CreatedAtUtc, second));
        Assert.Empty((await workspace.GetAgentTeamEditorAsync(id)).AgentIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_agent_or_team_replacement_requires_its_original_observation(bool agent) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var member = await CreateAgentAsync(workspace, "Original member");
        if (agent) {
            var stale = await workspace.GetAgentEditorAsync(member);
            stale.ExpectedUpdatedAtUtc = null;
            stale.Name = "Tokenless replacement";
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => workspace.SaveAgentAsync(stale));
            Assert.Equal("Original member", (await workspace.GetAgentEditorAsync(member)).Name);
        } else {
            var id = await workspace.SaveAgentTeamAsync(new() { Name = "Original team", AgentIds = [member] });
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => workspace.SaveAgentTeamAsync(new() {
                Id = id, Name = "Tokenless team replacement", AgentIds = []
            }));
            Assert.Equal([member], (await workspace.GetAgentTeamEditorAsync(id)).AgentIds);
        }
    }

    [Fact]
    public async Task Stale_whole_team_replacement_cannot_remove_a_concurrent_member_insert() {
        await using var app = await TestApplication.CreateAsync();
        await using var first = app.Services.CreateAsyncScope();
        await using var second = app.Services.CreateAsyncScope();
        var a = first.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var b = second.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var member = await CreateAgentAsync(a, "First member");
        var added = await CreateAgentAsync(a, "Concurrent member");
        var id = await a.SaveAgentTeamAsync(new() { Name = "Member race team", AgentIds = [member] });
        var stale = await a.GetAgentTeamEditorAsync(id);
        var peer = await b.GetAgentTeamEditorAsync(id);
        peer.AgentIds.Add(added);
        await b.SaveAgentTeamAsync(peer);
        stale.Description = "Stale aggregate metadata";
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => a.SaveAgentTeamAsync(stale));
        Assert.Equal(new[] { member, added }.Order(), (await a.GetAgentTeamEditorAsync(id)).AgentIds.Order());
    }

    [Fact]
    public async Task Stale_metadata_cannot_revert_a_concurrent_team_rename() {
        await using var app = await TestApplication.CreateAsync();
        await using var first = app.Services.CreateAsyncScope();
        await using var second = app.Services.CreateAsyncScope();
        var a = first.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var b = second.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await a.SaveAgentTeamAsync(new() { Name = "Original metadata team" });
        var stale = await a.GetAgentTeamEditorAsync(id);
        var peer = await b.GetAgentTeamEditorAsync(id);
        peer.Name = "Peer metadata team";
        await b.SaveAgentTeamMetadataAsync(peer);
        stale.Description = "My old-name draft";
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => a.SaveAgentTeamMetadataAsync(stale));
        Assert.Equal("Peer metadata team", (await a.GetAgentTeamEditorAsync(id)).Name);
    }

    [Fact]
    public async Task Membership_set_replacement_requires_its_original_observation() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var member = await CreateAgentAsync(workspace, "Retained member");
        var id = await workspace.SaveAgentTeamAsync(new() { Name = "Tokenless membership team", AgentIds = [member] });
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => workspace.UpdateAgentTeamMembersAsync(id, []));
        Assert.Equal([member], (await workspace.GetAgentTeamEditorAsync(id)).AgentIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Identity_only_delete_cannot_delete_an_existing_agent_or_team(bool agent) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = agent ? await CreateAgentAsync(workspace, "Retained agent")
            : await workspace.SaveAgentTeamAsync(new() { Name = "Retained team" });
        if (agent) {
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => workspace.DeleteAgentAsync(id));
            Assert.Equal(id, (await workspace.GetAgentEditorAsync(id)).Id);
        } else {
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => workspace.DeleteAgentTeamAsync(id));
            Assert.Equal(id, (await workspace.GetAgentTeamEditorAsync(id)).Id);
        }
    }

    private static async Task<Guid> CreateAgentAsync(IAgentFrameworkWorkspaceService workspace, string name) {
        var editor = await workspace.GetAgentEditorAsync();
        editor.Name = name;
        editor.RoleTitle = "Configuration test";
        editor.Summary = "Native original-observation fixture";
        editor.Instructions = "Keep the configured scope.";
        return await workspace.SaveAgentAsync(editor);
    }
}
