using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Web.Api;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.AgentFramework;
using System.Text.Json;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class CapabilityConfigurationConcurrencyTests {
    [Fact]
    public async Task An_assignment_to_a_deleted_capability_is_refused_without_saving_other_agent_fields() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var capability = await workspace.SaveCapabilityEditorAsync(new() {
            Kind = CapabilityKind.Skill, Key = "retired-assignment-capability", Name = "Retired assignment capability",
            EndpointOrPath = "inline://retired-assignment-capability", ConfigurationJson = """{"inlineSkill":{"instructions":"Safe fixture"}}"""
        });
        var agent = await workspace.SaveAgentDefinitionAsync(new() { Name = "Retired assignment agent" });
        var original = await workspace.GetAgentEditorAsync(agent.Id);
        await workspace.DeleteCapabilityAsync(capability.Id!.Value, capability.ExpectedFingerprint!);
        original.SelectedCapabilityIds = [capability.Id.Value];
        original.Summary = "Must not be committed";
        await Assert.ThrowsAsync<AgentEditorValidationException>(() => workspace.SaveAgentDefinitionAsync(original));
        var current = await workspace.GetAgentEditorAsync(agent.Id);
        Assert.Equal(agent.UpdatedAtUtc, current.ExpectedUpdatedAtUtc);
        Assert.Equal(string.Empty, current.Summary);
        Assert.Empty(current.SelectedCapabilityIds);
    }

    [Fact]
    public async Task Saving_an_original_deleted_elsewhere_is_a_conflict_and_cannot_recreate_it() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var original = await workspace.SaveCapabilityEditorAsync(new() {
            Kind = CapabilityKind.Skill, Key = "deleted-capability-original", Name = "Deleted capability original",
            EndpointOrPath = "inline://deleted-capability-original", ConfigurationJson = """{"inlineSkill":{"instructions":"Safe fixture"}}"""
        });
        await workspace.DeleteCapabilityAsync(original.Id!.Value, original.ExpectedFingerprint!);
        var rejected = await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => workspace.SaveCapabilityEditorAsync(original));
        Assert.True(rejected.IsConcurrencyConflict);
        Assert.DoesNotContain(await workspace.ListCapabilitiesAsync(), item => item.Id == original.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Editor_commands_cannot_create_or_promote_builtin_trust(bool existing) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var candidate = new CapabilityEditorModel {
            Kind = CapabilityKind.Skill, Key = "trust-boundary-capability", Name = "Trust boundary capability",
            EndpointOrPath = "inline://trust-boundary-capability", ConfigurationJson = """{"inlineSkill":{"instructions":"Safe fixture"}}"""
        };
        if (existing) {
            candidate = await workspace.SaveCapabilityEditorAsync(candidate);
        }
        candidate.IsBuiltIn = true;
        var rejected = await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => workspace.SaveCapabilityEditorAsync(candidate));
        Assert.False(rejected.IsConcurrencyConflict);
        Assert.DoesNotContain(await workspace.ListCapabilitiesAsync(), item => item.Key == candidate.Key && item.IsBuiltIn);
        if (existing) {
            var preserved = await workspace.GetCapabilityEditorAsync(candidate.Id);
            Assert.False(preserved.IsBuiltIn);
            Assert.Equal(candidate.ExpectedFingerprint, preserved.ExpectedFingerprint);
        } else {
            Assert.DoesNotContain(await workspace.ListCapabilitiesAsync(), item => item.Key == candidate.Key);
        }
    }

    [Fact]
    public async Task Native_capability_publication_is_commit_only_scoped_metadata_with_original_editor_identity() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var saved = await workspace.SaveCapabilityEditorAsync(new() {
            Kind = CapabilityKind.Skill, Key = "observed-capability", Name = "Observed capability",
            EndpointOrPath = "inline://observed-capability", ConfigurationJson = """{"inlineSkill":{"instructions":"Private configuration text retained only in the definition"}}"""
        });
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var topic = new EditingTopic(new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation), EditingOwner.AgentCapability, saved.Id);
        var feed = services.GetRequiredService<EditingChangeFeed>();
        using var observation = feed.Subscribe(topic);
        var editor = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var authorizations = 0;
        using (services.GetRequiredService<AgentConfigurationWriteContext>().Enter(topic.Scope, new(editor, operation), token => {
            token.ThrowIfCancellationRequested();
            authorizations++;
            return ValueTask.CompletedTask;
        })) {
            saved.Description = "Native accepted capability change";
            await workspace.SaveCapabilityEditorAsync(saved);
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var change = Assert.Single((await observation.ReadAsync(timeout.Token)).Changes);
        Assert.Equal(topic, change.Resource);
        Assert.Equal(editor, change.OriginEditorId);
        Assert.Equal(operation, change.OperationId);
        Assert.True(authorizations >= 2);
        Assert.DoesNotContain("Private configuration text", JsonSerializer.Serialize(change));
        var delivered = feed.DeliveredHints;
        await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => workspace.SaveCapabilityEditorAsync(saved));
        Assert.Equal(delivered, feed.DeliveredHints);
    }

    [Fact]
    public async Task HTTP_capability_originals_are_required_and_conflicts_are_typed() {
        await using var host = await ApiTestHost.CreateAsync(false, useInMemoryDatabase: true);
        using var create = await host.Client.PostAsJsonAsync("/api/agents/capabilities", new CapabilityEditorModel {
            Kind = CapabilityKind.Skill, Key = "http-concurrent-capability", Name = "HTTP capability",
            EndpointOrPath = "inline://http-concurrent-capability", ConfigurationJson = """{"inlineSkill":{"instructions":"Safe local fixture"}}"""
        });
        create.EnsureSuccessStatusCode();
        var id = await create.Content.ReadFromJsonAsync<Guid>();
        var path = $"/api/agents/capabilities/{id}";
        var original = await host.Client.GetFromJsonAsync<CapabilityEditorModel>($"{path}/editor") ?? throw new InvalidOperationException("Missing capability.");
        var fingerprint = original.ExpectedFingerprint;
        original.ExpectedFingerprint = null;
        using var missing = await host.Client.PostAsJsonAsync("/api/agents/capabilities", original);
        await Conflict(missing);
        original.ExpectedFingerprint = fingerprint;
        original.Description = "Accepted peer description";
        using var peer = await host.Client.PostAsJsonAsync("/api/agents/capabilities", original);
        peer.EnsureSuccessStatusCode();
        using var stale = await host.Client.PostAsJsonAsync("/api/agents/capabilities", original);
        await Conflict(stale);
        using var tokenlessDelete = await host.Client.DeleteAsync(path);
        await Conflict(tokenlessDelete);
        using var staleDelete = await host.Client.DeleteAsync($"{path}?expectedFingerprint={Uri.EscapeDataString(fingerprint!)}");
        await Conflict(staleDelete);
        var current = await host.Client.GetFromJsonAsync<CapabilityEditorModel>($"{path}/editor") ?? throw new InvalidOperationException("Missing current capability.");
        Assert.Equal("Accepted peer description", current.Description);
        using var delete = await host.Client.DeleteAsync($"{path}?expectedFingerprint={Uri.EscapeDataString(current.ExpectedFingerprint!)}");
        delete.EnsureSuccessStatusCode();

        static async Task Conflict(HttpResponseMessage response) {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
            Assert.Equal("agents.capability-configuration-conflict", Assert.Single(error!.Errors).Code);
        }
    }

    [Fact]
    public async Task Stale_capability_save_and_delete_preserve_the_peer_configuration() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var created = await workspace.SaveCapabilityEditorAsync(new() {
            Kind = CapabilityKind.Skill, Key = "conditional-capability", Name = "Conditional capability",
            EndpointOrPath = "inline://conditional-capability",
            ConfigurationJson = """{"inlineSkill":{"instructions":"Preserved instructions"},"future":17}"""
        });
        var peer = await workspace.GetCapabilityEditorAsync(created.Id);
        peer.Description = "Peer description";
        var accepted = await workspace.SaveCapabilityEditorAsync(peer);
        created.Name = "Stale replacement";
        Assert.True((await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => workspace.SaveCapabilityAsync(created))).IsConcurrencyConflict);
        Assert.True((await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => workspace.DeleteCapabilityAsync(created.Id!.Value, created.ExpectedFingerprint!))).IsConcurrencyConflict);
        var current = await workspace.GetCapabilityEditorAsync(created.Id);
        Assert.Equal("Peer description", current.Description);
        Assert.Equal(accepted.ExpectedFingerprint, current.ExpectedFingerprint);
        Assert.Contains("\"future\":17", current.ConfigurationJson);
        await workspace.DeleteCapabilityAsync(created.Id.Value, current.ExpectedFingerprint!);
        Assert.DoesNotContain(await workspace.ListCapabilitiesAsync(), capability => capability.Id == created.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_capability_replacement_and_deletion_require_the_original_fingerprint(bool delete) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await workspace.SaveCapabilityAsync(new() {
            Kind = CapabilityKind.Skill, Key = "concurrent-capability-original", Name = "Original capability",
            EndpointOrPath = "inline://concurrent-capability-original",
            ConfigurationJson = """{"inlineSkill":{"instructions":"Original instructions"}}"""
        });
        var original = await workspace.GetCapabilityEditorAsync(id);
        original.ExpectedFingerprint = null;
        original.Description = "Tokenless replacement";
        var rejected = await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => delete
            ? workspace.DeleteCapabilityAsync(id)
            : workspace.SaveCapabilityAsync(original));
        Assert.True(rejected.IsConcurrencyConflict);
        Assert.Equal(string.Empty, (await workspace.GetCapabilityEditorAsync(id)).Description);
    }
}
