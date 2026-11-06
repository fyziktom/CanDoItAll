using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Reflection;
using CanDoItAll.AgentFramework.Persistence;
using Diagnostics = CanDoItAll.Tests.Integration.SharedProviders.ProviderMutationCommitIntegrationTests.DiagnosticProxy;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkspaceProviderConcurrencyTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_file_catalog_observer_failure_preserves_the_actual_provider_commit(bool delete) {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var observer = new CatalogObserver();
        var store = new FileSandboxWorkspaceStore(fixture.RootPath, null, null, observer);
        var registry = new WorkspaceBackedProviderProfileRegistry(store, new ProviderProfileService(), fixture.Identity);
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        observer.Fail = true;
        observer.Calls = 0;
        original.Name = "Accepted file change despite observer";

        var failure = await Assert.ThrowsAnyAsync<Exception>(async () => {
            if (delete) {
                await registry.DeleteProviderAsync(id, original.ExpectedConcurrencyToken!.Value);
            } else {
                await registry.SaveProviderEditorAsync(original);
            }
        });

        Assert.True(Assert.IsAssignableFrom<IProviderConfigurationFailure>(failure).CanonicalCommitSucceeded);
        var outcome = Assert.IsType<ProviderConfigurationCommittedException>(failure);
        observer.Fail = false;
        var current = await registry.GetProviderAsync(id);
        Assert.Equal(delete, current is null);
        if (!delete) {
            Assert.Equal(original.Name, current!.Name);
            Assert.Equal(original.Name, outcome.Accepted!.Name);
            var acceptedToken = outcome.Accepted.ExpectedConcurrencyToken;
            var peer = await registry.GetProviderEditorAsync(id);
            peer.Name = "Later peer after the acknowledged file commit";
            await registry.SaveProviderAsync(peer);
            Assert.Equal(original.Name, outcome.Accepted.Name);
            Assert.NotEqual(acceptedToken, (await registry.GetProviderEditorAsync(id)).ExpectedConcurrencyToken);
        }
        Assert.Equal(delete ? 1 : 2, observer.Calls);
    }

    [Fact]
    public async Task Core_model_maintenance_reports_a_committed_default_when_the_file_observer_fails() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var observer = new CatalogObserver();
        var store = new FileSandboxWorkspaceStore(fixture.RootPath, null, null, observer);
        var registry = new WorkspaceBackedProviderProfileRegistry(store, new ProviderProfileService(), fixture.Identity);
        var id = await registry.SaveProviderAsync(NewEditor());
        var completed = new ProviderModelMaintenanceEditorResult("created-model", "owned-model", "Owned instruction", 4096, "Owned modelfile", "Created remotely");
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, Diagnostics>();
        var proxy = (Diagnostics)(object)diagnostics;
        proxy.Maintenance = _ => {
            observer.Fail = true;
            return Task.FromResult(completed);
        };
        var catalog = new AgentFrameworkWorkspaceCatalogService(store,
            DispatchProxy.Create<IAgentPackageService, UnusedCapabilityDependency>(), fixture.Proof,
            new ProviderProfileService(), diagnostics, registry, registry);

        var failure = await Assert.ThrowsAsync<ProviderModelMaintenancePersistenceException>(() => catalog.CreateOrUpdateProviderModelAsync(id,
            new("owned-model", "created-model", "Owned instruction", 4096)));

        Assert.Equal(ProviderModelMaintenancePersistenceState.CommittedWithWarning, failure.State);
        Assert.Same(completed, failure.Result);
        Assert.Equal("created-model", (await registry.GetProviderAsync(id))!.DefaultModel);
        Assert.Equal(1, proxy.MaintenanceCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Core_model_maintenance_keeps_remote_completion_and_conditionally_updates_its_original(bool peer) {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var registry = new WorkspaceBackedProviderProfileRegistry(fixture.Store, new ProviderProfileService(), fixture.Identity);
        var editor = NewEditor();
        editor.ConfigurationJson = "{\"ownedExtension\":\"preserved\"}";
        var id = await registry.SaveProviderAsync(editor);
        var original = await registry.GetProviderEditorAsync(id);
        var completed = new ProviderModelMaintenanceEditorResult("created-model", "owned-model", "Owned instruction", 4096, "Owned modelfile", "Created remotely");
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, Diagnostics>();
        var proxy = (Diagnostics)(object)diagnostics;
        proxy.Maintenance = async _ => {
            if (peer) {
                var current = await registry.GetProviderEditorAsync(id);
                current.Name = "Peer during Core model creation";
                current.DefaultModel = "peer-model";
                current.SuggestedModels = ["peer-model"];
                await registry.SaveProviderAsync(current);
            }
            return completed;
        };
        var catalog = new AgentFrameworkWorkspaceCatalogService(fixture.Store,
            DispatchProxy.Create<IAgentPackageService, UnusedCapabilityDependency>(), fixture.Proof,
            new ProviderProfileService(), diagnostics, registry, registry);
        var request = new ProviderModelMaintenanceEditorRequest("owned-model", "created-model", "Owned instruction", 4096);
        if (peer) {
            var failure = await Assert.ThrowsAsync<ProviderModelMaintenancePersistenceException>(() => catalog.CreateOrUpdateProviderModelAsync(id, request));
            Assert.Equal(ProviderModelMaintenancePersistenceState.Conflict, failure.State);
            Assert.Same(completed, failure.Result);
        } else {
            Assert.Same(completed, await catalog.CreateOrUpdateProviderModelAsync(id, request));
        }
        var accepted = await registry.GetProviderEditorAsync(id);
        Assert.Equal(peer ? "peer-model" : "created-model", accepted.DefaultModel);
        Assert.Equal(peer ? "Peer during Core model creation" : original.Name, accepted.Name);
        Assert.Contains("preserved", accepted.ConfigurationJson, StringComparison.Ordinal);
        Assert.NotEqual(original.ExpectedConcurrencyToken, accepted.ExpectedConcurrencyToken);
        Assert.Equal(1, proxy.MaintenanceCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Core_health_uses_its_original_and_never_imports_probe_models(bool peer) {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var registry = new WorkspaceBackedProviderProfileRegistry(fixture.Store, new ProviderProfileService(), fixture.Identity);
        var id = await registry.SaveProviderAsync(NewEditor());
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, Diagnostics>();
        var proxy = (Diagnostics)(object)diagnostics;
        proxy.Probe = async _ => {
            if (peer) {
                var current = await registry.GetProviderEditorAsync(id);
                current.Name = "Peer during Core probe";
                current.SuggestedModels = ["owned-model", "peer-model"];
                await registry.SaveProviderAsync(current);
            }
            return new(true, "Probe completed", ["probe-model"]);
        };
        var catalog = new AgentFrameworkWorkspaceCatalogService(fixture.Store,
            DispatchProxy.Create<IAgentPackageService, UnusedCapabilityDependency>(), fixture.Proof,
            new ProviderProfileService(), diagnostics, registry, registry);
        if (peer) {
            await Assert.ThrowsAsync<ProviderConfigurationConflictException>(() => catalog.TestProviderAsync(id));
        } else {
            await catalog.TestProviderAsync(id);
        }
        var currentProvider = await registry.GetProviderEditorAsync(id);
        Assert.DoesNotContain("probe-model", currentProvider.SuggestedModels);
        Assert.Contains("owned-model", currentProvider.SuggestedModels);
        Assert.Equal(peer ? "Peer during Core probe" : "Owned file provider", currentProvider.Name);
        Assert.Equal(1, proxy.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task File_provider_originals_are_bound_to_the_workspace_scope_and_generation(bool generation) {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var registry = new WorkspaceBackedProviderProfileRegistry(fixture.Store, new ProviderProfileService(), fixture.Identity);
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var otherIdentity = new AgentExecutionActivityWorkspaceIdentity(fixture.Identity.DatabaseProfileId,
            generation ? fixture.Identity.WorkspaceScope : new(WorkspaceScopeKind.Project, "other-provider-scope"),
            generation ? new(fixture.Identity.DatabaseProfileGeneration.Value + 1) : fixture.Identity.DatabaseProfileGeneration);
        var other = new WorkspaceBackedProviderProfileRegistry(fixture.Store, new ProviderProfileService(), otherIdentity);
        original.Name = "Wrong scope must not publish";
        await Assert.ThrowsAsync<ProviderConfigurationConflictException>(() => other.SaveProviderAsync(original));
        Assert.NotEqual(original.Name, (await registry.GetProviderEditorAsync(id)).Name);
    }

    [Fact]
    public async Task Conditional_file_provider_delete_requires_the_reviewed_version_and_never_recreates_a_deleted_original() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var registry = new WorkspaceBackedProviderProfileRegistry(fixture.Store, new ProviderProfileService(), fixture.Identity);
        var id = await registry.SaveProviderAsync(NewEditor());
        var stale = await registry.GetProviderEditorAsync(id);
        var peer = await registry.GetProviderEditorAsync(id);
        peer.Name = "Peer provider before delete";
        await registry.SaveProviderAsync(peer);
        await Assert.ThrowsAsync<ProviderConfigurationConflictException>(() => registry.DeleteProviderAsync(id, stale.ExpectedConcurrencyToken!.Value));
        var current = await registry.GetProviderEditorAsync(id);
        await registry.DeleteProviderAsync(id, current.ExpectedConcurrencyToken!.Value);
        Assert.Null(await registry.GetProviderAsync(id));
        await Assert.ThrowsAsync<ProviderConfigurationConflictException>(() => registry.SaveProviderAsync(stale));
        Assert.Null(await registry.GetProviderAsync(id));
    }

    [Fact]
    public async Task File_save_freezes_dispatch_and_returns_exact_acceptance_before_a_later_peer() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var direct = new WorkspaceBackedProviderProfileRegistry(fixture.Store, new ProviderProfileService(), fixture.Identity);
        var store = DispatchProxy.Create<ISandboxWorkspaceStore, HeldStore>();
        var held = (HeldStore)(object)store;
        held.Inner = fixture.Store;
        held.AfterCommit = async accepted => {
            var id = accepted.Providers.Single(provider => provider.Name == "Exact file acceptance").Id;
            var peer = await direct.GetProviderEditorAsync(id);
            peer.Name = "Later file peer";
            await direct.SaveProviderAsync(peer);
        };
        var registry = new WorkspaceBackedProviderProfileRegistry(store, new ProviderProfileService(), fixture.Identity);
        var draft = NewEditor();
        draft.Name = "  Exact file acceptance  ";
        var save = registry.SaveProviderEditorAsync(draft);
        await held.Started.Task;
        draft.Name = "Typed after dispatch";
        draft.Tags = ["later-tag"];
        held.Release.SetResult();
        var accepted = await save;
        var current = await direct.GetProviderEditorAsync(accepted.Id);
        Assert.Equal("Exact file acceptance", accepted.Name);
        Assert.DoesNotContain("later-tag", accepted.Tags);
        Assert.Equal("Typed after dispatch", draft.Name);
        Assert.Equal("Later file peer", current.Name);
        Assert.NotEqual(accepted.ExpectedConcurrencyToken, current.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Stale_file_provider_editor_cannot_overwrite_the_peer_configuration() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var registry = new WorkspaceBackedProviderProfileRegistry(fixture.Store, new ProviderProfileService(), fixture.Identity);
        var id = await registry.SaveProviderAsync(NewEditor());
        var stale = await registry.GetProviderEditorAsync(id);
        var peer = await registry.GetProviderEditorAsync(id);
        peer.Name = "Peer file provider";
        await registry.SaveProviderAsync(peer);
        stale.Name = "Stale file provider";

        await Assert.ThrowsAsync<ProviderConfigurationConflictException>(() => registry.SaveProviderAsync(stale));

        Assert.Equal(peer.Name, (await registry.GetProviderEditorAsync(id)).Name);
    }

    [Fact]
    public async Task Identity_only_file_provider_delete_cannot_remove_a_current_configuration() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var registry = new WorkspaceBackedProviderProfileRegistry(fixture.Store, new ProviderProfileService(), fixture.Identity);
        var id = await registry.SaveProviderAsync(NewEditor());

        await Assert.ThrowsAsync<ProviderConfigurationConflictException>(() => registry.DeleteProviderAsync(id));

        Assert.NotNull(await registry.GetProviderAsync(id));
    }

    private static ProviderProfileEditorModel NewEditor() => new() {
        Name = "Owned file provider", Kind = ProviderKind.Ollama, BaseUrl = "http://127.0.0.1:18434",
        DefaultModel = "owned-model", SuggestedModels = ["owned-model"], Transport = ProviderTransportKind.ChatCompletions
    };

    public class HeldStore : DispatchProxy {
        public ISandboxWorkspaceStore Inner { get; set; } = null!;
        public Func<SandboxWorkspaceCatalog, Task> AfterCommit { get; set; } = _ => Task.CompletedTask;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(ISandboxWorkspaceStore.UpdateCatalogAsync)) {
                return UpdateAsync((Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog>)args![0]!, (CancellationToken)args[1]!);
            }
            return targetMethod!.Invoke(Inner, args);
        }
        private async Task<SandboxWorkspaceCatalog> UpdateAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update, CancellationToken token) {
            Started.SetResult();
            await Release.Task.WaitAsync(token);
            var accepted = await Inner.UpdateCatalogAsync(update, token);
            await AfterCommit(accepted);
            return accepted;
        }
    }

    private sealed class CatalogObserver : IAgentCatalogCommitObserver {
        public bool Fail { get; set; }
        public int Calls { get; set; }
        public void Committed(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after) {
            Calls++;
            if (Fail) {
                throw new IOException("Synthetic catalog observer failure after primary publication.");
            }
        }
    }
}
