using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class CapabilityCatalogSubmissionTests {
    [Fact]
    public async Task Native_editor_returns_committed_identity_and_fingerprint_and_refuses_stale_update() {
        var store = DispatchProxy.Create<ISandboxWorkspaceStore, HeldCatalog>();
        var held = (HeldCatalog)(object)store;
        held.Release.SetResult();
        var owner = new AgentFrameworkWorkspaceCatalogService(store, null!, null!, null!, null!, null!, null!);
        var accepted = await owner.SaveCapabilityEditorAsync(new() {
            Kind = CapabilityKind.McpServer, Key = "receipt", Name = " Receipt ", ConfigurationJson = "{}"
        });
        Assert.NotNull(accepted.Id);
        Assert.Equal("Receipt", accepted.Name);
        Assert.Equal(CapabilityEditorConcurrency.ComputeFingerprint(accepted), accepted.ExpectedFingerprint);
        var stale = CapabilityEditorModel.FromDefinition(Assert.Single(held.Catalog.Capabilities));
        stale.ExpectedFingerprint = accepted.ExpectedFingerprint;
        accepted.Name = "Another operator";
        var updated = await owner.SaveCapabilityEditorAsync(accepted);
        Assert.NotEqual(stale.ExpectedFingerprint, updated.ExpectedFingerprint);
        stale.Description = "Stale save";
        var conflict = await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => owner.SaveCapabilityEditorAsync(stale));
        Assert.True(conflict.IsConcurrencyConflict);
        Assert.Equal("Another operator", Assert.Single(held.Catalog.Capabilities).Name);
    }

    [Fact]
    public async Task Missing_update_duplicate_key_and_fingerprinted_create_are_refused_without_writes() {
        var store = DispatchProxy.Create<ISandboxWorkspaceStore, HeldCatalog>();
        var held = (HeldCatalog)(object)store;
        held.Release.SetResult();
        var owner = new AgentFrameworkWorkspaceCatalogService(store, null!, null!, null!, null!, null!, null!);
        await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => owner.SaveCapabilityEditorAsync(new() {
            Id = Guid.NewGuid(), Kind = CapabilityKind.McpServer, Key = "missing", Name = "Missing"
        }));
        Assert.Empty(held.Catalog.Capabilities);
        await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => owner.SaveCapabilityEditorAsync(new() {
            ExpectedFingerprint = "stale", Kind = CapabilityKind.McpServer, Key = "new", Name = "New"
        }));
        var accepted = await owner.SaveCapabilityEditorAsync(new() { Kind = CapabilityKind.McpServer, Key = "unique", Name = "Original" });
        await Assert.ThrowsAsync<CapabilityCatalogRejectedException>(() => owner.SaveCapabilityEditorAsync(new() {
            Kind = CapabilityKind.McpServer, Key = "unique", Name = "Duplicate"
        }));
        Assert.Equal(accepted.Id, Assert.Single(held.Catalog.Capabilities).Id);
    }

    [Fact]
    public async Task Native_save_freezes_the_definition_before_waiting_for_catalog_coordination() {
        var store = DispatchProxy.Create<ISandboxWorkspaceStore, HeldCatalog>();
        var held = (HeldCatalog)(object)store;
        var owner = new AgentFrameworkWorkspaceCatalogService(store, null!, null!, null!, null!, null!, null!);
        var model = new CapabilityEditorModel {
            Kind = CapabilityKind.McpServer, Key = "captured", Name = "Original",
            ConfigurationJson = """{"transport":"logical"}""", Tags = ["original"]
        };
        var save = owner.SaveCapabilityAsync(model);
        Assert.True(held.Entered);
        model.Name = "Later edit";
        model.Tags.Add("later");
        held.Release.SetResult();
        var id = await save;
        var saved = Assert.Single(held.Catalog.Capabilities);
        Assert.Equal(id, saved.Id);
        Assert.Equal("Original", saved.Name);
        Assert.Equal(["original"], saved.Tags);
    }

    public class HeldCatalog : DispatchProxy {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Entered { get; private set; }
        public SandboxWorkspaceCatalog Catalog { get; private set; } = SandboxWorkspaceCatalog.Empty;
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name == nameof(ISandboxWorkspaceStore.UpdateCatalogAsync)) {
                Entered = true;
                return UpdateAsync((Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog>)args![0]!);
            }
            throw new InvalidOperationException("Unexpected catalog call.");
        }
        private async Task<SandboxWorkspaceCatalog> UpdateAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update) {
            await Release.Task;
            Catalog = update(Catalog);
            return Catalog;
        }
    }
}
