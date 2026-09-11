using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class AgentCatalogReadLeaseTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Source_and_executor_share_one_snapshot_and_either_revocation_waits_for_release(bool revokeExecutor, bool independentWriter) {
        var root = TestFileSystem.CreateTemporaryRoot("workflow-source-executor-lease");
        try {
            var store = new FileSandboxWorkspaceStore(root);
            var source = await AddAgentAsync(store);
            var executor = await AddAgentAsync(store);
            var unselected = await AddAgentAsync(store);
            var writer = independentWriter ? new FileSandboxWorkspaceStore(root) : store;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var held = await store.AcquireAgentsReadLeaseAsync([source.Id, executor.Id], deadline.Token);
            Assert.NotEqual(source.Id, executor.Id);
            Assert.Equal(source.Id, held.Agent!.Id);
            Assert.Equal(new[] { source.Id, executor.Id }, held.Agents.Select(agent => agent.Id));
            Assert.DoesNotContain(held.Agents, agent => agent.Id == unselected.Id);
            var revokedId = revokeExecutor ? executor.Id : source.Id;
            using (var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(150))) {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.UpdateCatalogAsync(Revoke, cancelled.Token));
            }
            Assert.All(held.Agents, agent => Assert.True(agent.Permissions.CanUseTools));
            await held.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => held.Agents);
            await writer.UpdateCatalogAsync(Revoke, deadline.Token);
            await using var next = await new FileSandboxWorkspaceStore(root).AcquireAgentsReadLeaseAsync([source.Id, executor.Id], deadline.Token);
            Assert.False(Assert.Single(next.Agents, agent => agent.Id == revokedId).Permissions.CanUseTools);
            Assert.True(Assert.Single(next.Agents, agent => agent.Id != revokedId).Permissions.CanUseTools);

            SandboxWorkspaceCatalog Revoke(SandboxWorkspaceCatalog catalog) => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == revokedId
                    ? agent with { Permissions = agent.Permissions with { CanUseTools = false } } : agent).ToArray()
            };
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public async Task Bounded_selection_retains_exact_missing_primary_and_rejects_duplicates_or_more_than_two() {
        var root = TestFileSystem.CreateTemporaryRoot("workflow-bounded-catalog-lease");
        try {
            var store = new FileSandboxWorkspaceStore(root);
            var executor = await AddAgentAsync(store);
            await Assert.ThrowsAsync<ArgumentException>(() => store.AcquireAgentsReadLeaseAsync([]));
            await Assert.ThrowsAsync<ArgumentException>(() => store.AcquireAgentsReadLeaseAsync([executor.Id, executor.Id]));
            await Assert.ThrowsAsync<ArgumentException>(() => store.AcquireAgentsReadLeaseAsync([executor.Id, Guid.NewGuid(), Guid.NewGuid()]));
            await using var held = await store.AcquireAgentsReadLeaseAsync([Guid.NewGuid(), executor.Id]);
            Assert.Null(held.Agent);
            Assert.Equal(executor.Id, Assert.Single(held.Agents).Id);
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }
}
