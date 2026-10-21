using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentCatalogAuthoritySnapshotTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unrelated_edits_commit_while_selected_source_and_executor_remain_held(bool independentWriter) {
        await using var fixture = await Fixture.CreateAsync();
        var writer = independentWriter ? new FileSandboxWorkspaceStore(fixture.Root) : fixture.Store;
        await using var held = await fixture.Store.AcquireAgentAuthoritySnapshotAsync([fixture.Source.Id, fixture.Executor.Id]);
        var revision = held.Revision;
        Assert.Equal(fixture.Source.Id, held.Agent!.Id);
        Assert.Equal(new[] { fixture.Source.Id, fixture.Executor.Id }, held.Agents.Select(item => item.Id));
        Assert.IsType<ImmutableArray<string>>(held.Agent.Tags);
        Assert.IsType<ImmutableArray<AgentCapabilityAssignment>>(held.Agent.Capabilities);
        Assert.IsType<ImmutableArray<AgentAllowedSecretReference>>(held.Agent.Permissions.NormalizedAllowedSecrets);
        var changed = await writer.UpdateCatalogAsync(catalog => Rename(catalog, fixture.Unrelated.Id), fixture.Token);
        Assert.True(changed.CatalogDataRevision.Value > revision.Value);
        Assert.Equal(revision, held.Revision);
        foreach (var selected in held.Agents) {
            using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.UpdateCatalogAsync(catalog =>
                catalog with { Agents = catalog.Agents.Select(item => item.Id == selected.Id
                    ? item with { Permissions = item.Permissions with { CanUseTools = false } } : item).ToArray() }, cancelled.Token));
        }
        Assert.All(held.Agents, item => Assert.True(item.Permissions.CanUseTools));
        await held.DisposeAsync();
        await held.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => held.Agent);
        Assert.Throws<ObjectDisposedException>(() => held.Agents);
        Assert.Throws<ObjectDisposedException>(() => held.Scope);
        Assert.Throws<ObjectDisposedException>(() => held.Revision);
        await writer.UpdateCatalogAsync(catalog => Rename(catalog, fixture.Source.Id), fixture.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_capability_changes_and_selected_deletion_cannot_bypass_the_keys(bool deleteAgent) {
        await using var fixture = await Fixture.CreateAsync();
        var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Tool, "authority-snapshot",
            "Original", "Original", string.Empty, "{}", CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UtcNow, false);
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with { Capabilities = [.. catalog.Capabilities, capability] });
        var writer = new FileSandboxWorkspaceStore(fixture.Root);
        await using var held = await fixture.Store.AcquireAgentAuthoritySnapshotAsync([fixture.Source.Id]);
        using (var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(150))) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Change(cancelled.Token));
        }
        Assert.Equal(fixture.Source.Id, held.Agent!.Id);
        await held.DisposeAsync();
        await Change(fixture.Token);

        async Task Change(CancellationToken token) {
            if (deleteAgent) {
                await writer.DeleteAgentWorkspaceDataAsync(fixture.Source.Id, token);
            } else {
                await writer.UpdateCatalogAsync(catalog => catalog with {
                    Capabilities = catalog.Capabilities.Select(item => item.Id == capability.Id ? item with { Description = "Changed" } : item).ToArray()
                }, token);
            }
        }
    }

    [Fact]
    public async Task Missing_primary_is_an_exact_locked_absence_without_substitution() {
        await using var fixture = await Fixture.CreateAsync();
        var missing = fixture.Source with { Id = Guid.NewGuid() };
        await using var held = await fixture.Store.AcquireAgentAuthoritySnapshotAsync([missing.Id, fixture.Executor.Id]);
        Assert.Null(held.Agent);
        Assert.Equal(fixture.Executor.Id, Assert.Single(held.Agents).Id);
        var writer = new FileSandboxWorkspaceStore(fixture.Root);
        using (var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(150))) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                writer.UpdateCatalogAsync(catalog => catalog with { Agents = [.. catalog.Agents, missing] }, cancelled.Token));
        }
        await writer.UpdateCatalogAsync(catalog => Rename(catalog, fixture.Unrelated.Id), fixture.Token);
        await held.DisposeAsync();
        await writer.UpdateCatalogAsync(catalog => catalog with { Agents = [.. catalog.Agents, missing] });
        await using var current = await writer.AcquireAgentAuthoritySnapshotAsync([missing.Id]);
        Assert.Equal(missing.Id, current.Agent!.Id);
    }

    [Fact]
    public async Task Cancellation_while_waiting_releases_already_acquired_keys() {
        await using var fixture = await Fixture.CreateAsync();
        var ordered = new[] { fixture.Source.Id, fixture.Executor.Id }.OrderBy(id => id.ToString("N"), StringComparer.Ordinal).ToArray();
        await using var blocked = await fixture.Store.AcquireAgentAuthoritySnapshotAsync([ordered[1]]);
        var other = new FileSandboxWorkspaceStore(fixture.Root);
        using (var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(150))) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                other.AcquireAgentAuthoritySnapshotAsync([ordered[1], ordered[0]], cancelled.Token));
        }
        await using var released = await other.AcquireAgentAuthoritySnapshotAsync([ordered[0]], fixture.Token);
        Assert.Equal(ordered[0], released.Agent!.Id);
        Assert.Equal(ordered[1], blocked.Agent!.Id);
    }

    [Fact]
    public async Task Reversed_overlapping_selections_complete_without_lock_order_deadlock() {
        await using var fixture = await Fixture.CreateAsync();
        var other = new FileSandboxWorkspaceStore(fixture.Root);
        await Task.WhenAll(Read(fixture.Store, [fixture.Source.Id, fixture.Executor.Id]),
            Read(other, [fixture.Executor.Id, fixture.Source.Id]));

        async Task Read(FileSandboxWorkspaceStore store, Guid[] selected) {
            await using var held = await store.AcquireAgentAuthoritySnapshotAsync(selected, fixture.Token);
            Assert.Equal(selected[0], held.Agent!.Id);
            Assert.Equal(selected, held.Agents.Select(item => item.Id));
            await Task.Yield();
        }
    }

    [Fact]
    public async Task Failed_catalog_read_releases_coordination_and_preserves_failure() {
        await using var fixture = await Fixture.CreateAsync();
        var path = Path.Combine(WorkspaceScopeDescriptor.Sandbox.ResolveDataRoot(fixture.Root), "workspace.json");
        var original = await File.ReadAllBytesAsync(path);
        try {
            await File.WriteAllTextAsync(path, "{ invalid catalog }");
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.AcquireAgentAuthoritySnapshotAsync([fixture.Source.Id]));
        } finally {
            await File.WriteAllBytesAsync(path, original);
        }
        await using var held = await new FileSandboxWorkspaceStore(fixture.Root)
            .AcquireAgentAuthoritySnapshotAsync([fixture.Source.Id], fixture.Token);
        Assert.Equal(fixture.Source.Id, held.Agent!.Id);
    }

    [Fact]
    public async Task Invalid_selection_and_unsupported_optional_capabilities_fail_explicitly() {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Store.AcquireAgentAuthoritySnapshotAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.AcquireAgentAuthoritySnapshotAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.AcquireAgentAuthoritySnapshotAsync([Guid.Empty]));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.AcquireAgentAuthoritySnapshotAsync([fixture.Source.Id, fixture.Source.Id]));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.AcquireAgentAuthoritySnapshotAsync([fixture.Source.Id, fixture.Executor.Id, fixture.Unrelated.Id]));
        await using var held = await fixture.Store.AcquireAgentAuthoritySnapshotAsync([fixture.Source.Id]);
        Assert.Throws<NotSupportedException>(() => held.Capabilities);
        await Assert.ThrowsAsync<NotSupportedException>(() => held.AcquireExecutionAdmissionReadLeaseAsync(fixture.Store, Guid.NewGuid()));
        var legacy = new LegacyStore();
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            ((IAgentCatalogReadLeaseStore)legacy).AcquireAgentAuthoritySnapshotAsync([fixture.Source.Id]));
        Assert.Equal(0, legacy.CoarseCalls);
    }

    private static SandboxWorkspaceCatalog Rename(SandboxWorkspaceCatalog catalog, Guid id)
        => catalog with { Agents = catalog.Agents.Select(item => item.Id == id ? item with { Name = "Changed" } : item).ToArray() };

    private sealed class LegacyStore : IAgentCatalogReadLeaseStore {
        internal int CoarseCalls { get; private set; }
        public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default) {
            CoarseCalls++;
            throw new InvalidOperationException("The coarse capability must not be used.");
        }
    }

    private sealed record Fixture(string Root, FileSandboxWorkspaceStore Store, AgentDefinition Source,
        AgentDefinition Executor, AgentDefinition Unrelated) : IAsyncDisposable {
        private readonly CancellationTokenSource deadline = new(TimeSpan.FromSeconds(15));
        internal CancellationToken Token => deadline.Token;

        internal static async Task<Fixture> CreateAsync() {
            var root = TestFileSystem.CreateTemporaryRoot("agent-catalog-read-lease");
            try {
                var store = new FileSandboxWorkspaceStore(root);
                var template = (await store.LoadCatalogAsync()).Agents.First();
                var agents = Enumerable.Range(0, 3).Select(index => template with {
                    Id = Guid.NewGuid(), Name = $"Authority fixture {index}", IsTemplate = false, TemplateKey = string.Empty,
                    ConfigurationJson = "{}", Tags = [], Status = AgentLifecycleStatus.Active,
                    Permissions = AgentPermissionsPolicy.Default with { CanUseTools = true, AllowedSecrets = [] }
                }).ToArray();
                await store.UpdateCatalogAsync(catalog => catalog with { Agents = [.. catalog.Agents, .. agents] });
                return new(root, store, agents[0], agents[1], agents[2]);
            } catch {
                TestFileSystem.DeleteDirectoryWithRetry(root);
                throw;
            }
        }
        public ValueTask DisposeAsync() {
            deadline.Dispose();
            TestFileSystem.DeleteDirectoryWithRetry(Root);
            return ValueTask.CompletedTask;
        }
    }
}
