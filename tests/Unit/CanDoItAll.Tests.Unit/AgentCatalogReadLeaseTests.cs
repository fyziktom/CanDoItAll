using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentCatalogReadLeaseTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Held_catalog_snapshot_blocks_same_and_independent_store_writes_until_disposal(bool independentStore) {
        var root = TestFileSystem.CreateTemporaryRoot("agent-catalog-read-lease");
        try {
            var scope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"));
            var store = new FileSandboxWorkspaceStore(root, scope);
            var agent = await AddAgentAsync(store);
            var writer = independentStore ? new FileSandboxWorkspaceStore(root, scope) : store;
            await using var held = await store.AcquireAgentReadLeaseAsync(agent.Id);
            var revision = held.Revision;
            Assert.Equal(scope, held.Scope);
            Assert.Equal(agent.Id, held.Agent!.Id);
            Assert.IsType<ImmutableArray<string>>(held.Agent.Tags);
            Assert.IsType<ImmutableArray<AgentCapabilityAssignment>>(held.Agent.Capabilities);
            Assert.IsType<ImmutableArray<AgentAllowedSecretReference>>(held.Agent.Permissions.NormalizedAllowedSecrets);
            using (var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(150))) {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.UpdateCatalogAsync(
                    catalog => catalog with { Agents = catalog.Agents.Select(item => item.Id == agent.Id
                        ? item with { Permissions = item.Permissions with { CanUseTools = false } } : item).ToArray() }, cancelled.Token));
            }
            Assert.True(held.Agent.Permissions.CanUseTools);
            Assert.Equal(revision, held.Revision);
            await held.DisposeAsync();
            await held.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => held.Agent);
            var changed = await writer.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(item => item.Id == agent.Id
                    ? item with { Permissions = item.Permissions with { CanUseTools = false } } : item).ToArray()
            });
            Assert.False(changed.Agents.Single(item => item.Id == agent.Id).Permissions.CanUseTools);
            Assert.True(changed.CatalogDataRevision.Value > revision.Value);
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public async Task Missing_agent_is_an_exact_locked_absence_and_does_not_select_another_agent() {
        var root = TestFileSystem.CreateTemporaryRoot("agent-catalog-missing-lease");
        try {
            var store = new FileSandboxWorkspaceStore(root);
            var agent = await AddAgentAsync(store);
            await using var held = await store.AcquireAgentReadLeaseAsync(Guid.NewGuid());
            Assert.Null(held.Agent);
            Assert.True(held.Revision.IsAssigned);
            using (var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(150))) {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AcquireAgentReadLeaseAsync(agent.Id, cancelled.Token));
            }
            await held.DisposeAsync();
            await using var next = await store.AcquireAgentReadLeaseAsync(agent.Id);
            Assert.Equal(agent.Id, next.Agent!.Id);
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public async Task Failed_catalog_read_releases_both_locks_and_preserves_the_read_failure() {
        var root = TestFileSystem.CreateTemporaryRoot("agent-catalog-read-failure");
        try {
            var store = new FileSandboxWorkspaceStore(root);
            var agent = await AddAgentAsync(store);
            var path = Path.Combine(WorkspaceScopeDescriptor.Sandbox.ResolveDataRoot(root), "workspace.json");
            var original = await File.ReadAllBytesAsync(path);
            await File.WriteAllTextAsync(path, "{ invalid catalog }");
            await Assert.ThrowsAsync<InvalidDataException>(() => store.AcquireAgentReadLeaseAsync(agent.Id));
            await File.WriteAllBytesAsync(path, original);
            await using var held = await store.AcquireAgentReadLeaseAsync(agent.Id);
            Assert.Equal(agent.Id, held.Agent!.Id);
            await held.DisposeAsync();
            await using var independent = await new FileSandboxWorkspaceStore(root).AcquireAgentReadLeaseAsync(agent.Id);
            Assert.Equal(agent.Id, independent.Agent!.Id);
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public async Task Invalid_or_cancelled_acquisition_does_not_leave_a_held_catalog_lock() {
        var root = TestFileSystem.CreateTemporaryRoot("agent-catalog-lease-cancel");
        try {
            var store = new FileSandboxWorkspaceStore(root);
            var agent = await AddAgentAsync(store);
            await Assert.ThrowsAsync<ArgumentException>(() => store.AcquireAgentReadLeaseAsync(Guid.Empty));
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AcquireAgentReadLeaseAsync(agent.Id, cancelled.Token));
            await using var held = await store.AcquireAgentReadLeaseAsync(agent.Id);
            Assert.Equal(agent.Id, held.Agent!.Id);
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    private static async Task<AgentDefinition> AddAgentAsync(FileSandboxWorkspaceStore store) {
        var catalog = await store.LoadCatalogAsync();
        var agent = catalog.Agents.First() with {
            Id = Guid.NewGuid(), Name = "Catalog lease fixture", IsTemplate = false, TemplateKey = string.Empty,
            ConfigurationJson = "{}", Tags = [], Status = AgentLifecycleStatus.Active,
            Permissions = AgentPermissionsPolicy.Default with { CanUseTools = true, AllowedSecrets = [] }
        };
        await store.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, agent] });
        return agent;
    }
}
