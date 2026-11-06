using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentCatalogCommitAcceptanceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Copy_commands_keep_the_new_identity_after_actual_publication_failure_and_preserve_the_source(bool template) {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var observer = new Observer { Fail = true };
        var catalog = CreateCatalog(fixture, observer);
        var original = await catalog.GetAgentEditorAsync(fixture.Agent.Id);
        var failure = await Assert.ThrowsAsync<AgentCatalogCommittedException<Guid>>(() => template
            ? catalog.ConvertToTemplateAsync(fixture.Agent.Id, "owned-copy-template")
            : catalog.CloneAgentAsync(fixture.Agent.Id, "Owned copy clone"));
        observer.Fail = false;
        var copies = await catalog.ListAgentsAsync(true);
        var accepted = Assert.Single(copies, item => item.Id == failure.Accepted);
        Assert.NotEqual(fixture.Agent.Id, accepted.Id);
        Assert.Equal(template, accepted.IsTemplate);
        Assert.Equal(original.Name, (await catalog.GetAgentEditorAsync(fixture.Agent.Id)).Name);
        Assert.Equal(original.ExpectedUpdatedAtUtc, (await catalog.GetAgentEditorAsync(fixture.Agent.Id)).ExpectedUpdatedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_team_or_capability_delete_keeps_its_known_commit_after_real_file_observer_failure(bool team) {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var observer = new Observer();
        var catalog = CreateCatalog(fixture, observer);
        var originalTeam = team ? await catalog.SaveAgentTeamMetadataAsync(new() { Name = "Owned delete acceptance team" }) : null;
        var originalCapability = team ? null : await catalog.GetCapabilityEditorAsync(fixture.Capability.Id);
        var id = team ? originalTeam!.Id!.Value : fixture.Capability.Id;
        observer.Fail = true;
        var failure = await Assert.ThrowsAsync<AgentCatalogCommittedException<Guid>>(async () => {
            if (team) {
                await catalog.DeleteAgentTeamAsync(id, originalTeam!.ExpectedUpdatedAtUtc!.Value);
            } else {
                await catalog.DeleteCapabilityAsync(id, originalCapability!.ExpectedFingerprint!);
            }
        });
        Assert.Equal(id, failure.Accepted);
        observer.Fail = false;
        if (team) {
            Assert.DoesNotContain(await catalog.ListAgentTeamsAsync(), item => item.Id == id);
        } else {
            Assert.DoesNotContain(await catalog.ListCapabilitiesAsync(), item => item.Id == id);
            Assert.DoesNotContain((await catalog.GetAgentEditorAsync(fixture.Agent.Id)).SelectedCapabilityIds, item => item == id);
        }
    }

    [Fact]
    public async Task An_agent_save_keeps_its_exact_acceptance_after_a_real_file_publication_observer_fails() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var observer = new Observer();
        var catalog = CreateCatalog(fixture, observer);
        var original = await catalog.GetAgentEditorAsync(fixture.Agent.Id);
        original.Name = "Accepted agent with secondary warning";
        observer.Fail = true;
        var failure = await Assert.ThrowsAsync<AgentCatalogCommittedException<AgentDefinition>>(() => catalog.SaveAgentDefinitionAsync(original));
        Assert.Equal(fixture.Agent.Id, failure.Accepted.Id);
        Assert.Equal(original.Name, failure.Accepted.Name);
        observer.Fail = false;
        var peer = await catalog.GetAgentEditorAsync(fixture.Agent.Id);
        Assert.Equal(failure.Accepted.UpdatedAtUtc, peer.ExpectedUpdatedAtUtc);
        peer.Name = "Later peer agent";
        await catalog.SaveAgentDefinitionAsync(peer);
        Assert.Equal(original.Name, failure.Accepted.Name);
        Assert.NotEqual(failure.Accepted.UpdatedAtUtc, (await catalog.GetAgentEditorAsync(fixture.Agent.Id)).ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task A_team_metadata_save_keeps_its_exact_acceptance_after_a_real_file_publication_observer_fails() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var observer = new Observer();
        var catalog = CreateCatalog(fixture, observer);
        var original = await catalog.SaveAgentTeamMetadataAsync(new() { Name = "Original team" });
        original.Name = "Accepted team with secondary warning";
        observer.Fail = true;
        var failure = await Assert.ThrowsAsync<AgentCatalogCommittedException<AgentTeamEditorModel>>(() => catalog.SaveAgentTeamMetadataAsync(original));
        Assert.Equal(original.Id, failure.Accepted.Id);
        Assert.Equal(original.Name, failure.Accepted.Name);
        observer.Fail = false;
        var peer = await catalog.GetAgentTeamEditorAsync(original.Id);
        Assert.Equal(failure.Accepted.ExpectedUpdatedAtUtc, peer.ExpectedUpdatedAtUtc);
        peer.Name = "Later peer team";
        await catalog.SaveAgentTeamMetadataAsync(peer);
        Assert.Equal(original.Name, failure.Accepted.Name);
        Assert.NotEqual(failure.Accepted.ExpectedUpdatedAtUtc, (await catalog.GetAgentTeamEditorAsync(original.Id)).ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task A_capability_save_keeps_its_exact_acceptance_after_a_real_file_publication_observer_fails() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var observer = new Observer();
        var catalog = CreateCatalog(fixture, observer);
        var original = await catalog.GetCapabilityEditorAsync(fixture.Capability.Id);
        original.Name = "Accepted capability with secondary warning";
        observer.Fail = true;
        var failure = await Assert.ThrowsAsync<AgentCatalogCommittedException<CapabilityEditorModel>>(() => catalog.SaveCapabilityEditorAsync(original));
        Assert.Equal(original.Id, failure.Accepted.Id);
        Assert.Equal(original.Name, failure.Accepted.Name);
        observer.Fail = false;
        var peer = await catalog.GetCapabilityEditorAsync(fixture.Capability.Id);
        Assert.Equal(failure.Accepted.ExpectedFingerprint, peer.ExpectedFingerprint);
        peer.Name = "Later peer capability";
        await catalog.SaveCapabilityEditorAsync(peer);
        Assert.Equal(original.Name, failure.Accepted.Name);
        Assert.NotEqual(failure.Accepted.ExpectedFingerprint, (await catalog.GetCapabilityEditorAsync(fixture.Capability.Id)).ExpectedFingerprint);
    }

    private static AgentFrameworkWorkspaceCatalogService CreateCatalog(CapabilityFileFixture fixture, Observer observer) {
        var store = new FileSandboxWorkspaceStore(fixture.RootPath, null, null, observer);
        var profiles = new ProviderProfileService();
        var registry = new WorkspaceBackedProviderProfileRegistry(store, profiles, fixture.Identity);
        return new(store, DispatchProxy.Create<IAgentPackageService, UnusedCapabilityDependency>(), fixture.Proof,
            profiles, DispatchProxy.Create<IProviderDiagnosticsService, UnusedCapabilityDependency>(), registry, registry);
    }

    private sealed class Observer : IAgentCatalogCommitObserver {
        public bool Fail { get; set; }
        public void Committed(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after) {
            if (Fail) {
                throw new IOException("Owned secondary failure after actual primary catalog publication.");
            }
        }
    }
}
