using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentDecisionDataPolicyOwnerTests {
    [Fact]
    public async Task Unsupported_catalog_owner_refuses_without_falling_back_to_any_legacy_writer() {
        var store = new LegacyOwner();
        var workspace = Identity();
        var owner = new AgentDecisionDataPolicyOwner(store, workspace, new FixedAgentExecutionProfileGenerationSource(new(1)));
        var command = Command(workspace);
        var result = await owner.ReplaceAsync(command);
        Assert.Equal(AgentDecisionDataPolicyRejection.Unsupported,
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(result).Reason);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Wrong_original_workspace_or_generation_refuses_before_owner_io() {
        var store = new LegacyOwner();
        var workspace = Identity();
        var owner = new AgentDecisionDataPolicyOwner(store, workspace, new FixedAgentExecutionProfileGenerationSource(new(1)));
        Assert.Equal(AgentDecisionDataPolicyRejection.ContextChanged,
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(await owner.ReplaceAsync(Command(Identity()))).Reason);
        var retired = new AgentDecisionDataPolicyOwner(store, workspace, new FixedAgentExecutionProfileGenerationSource(new(2)));
        Assert.Equal(AgentDecisionDataPolicyRejection.ContextChanged,
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(await retired.ReplaceAsync(Command(workspace))).Reason);
        await Assert.ThrowsAsync<AgentDecisionDataPolicyContextChangedException>(() => retired.ReadAsync(Guid.NewGuid()));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Unexpected_store_failure_retains_original_intent_and_is_not_claimed_as_rejected() {
        var workspace = Identity();
        var command = Command(workspace);
        var owner = new AgentDecisionDataPolicyOwner(new ThrowingOwner(), workspace,
            new FixedAgentExecutionProfileGenerationSource(new(1)));
        var result = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Unconfirmed>(await owner.ReplaceAsync(command));
        Assert.Same(command, result.Command);
        Assert.NotNull(result.Failure);
        Assert.Equal(AgentDecisionDataPolicyFailureStage.OwnerDispatch, result.Failure.Stage);
        Assert.Equal(new[] { typeof(NotSupportedException).FullName }, result.Failure.ExceptionTypes);
        var bounded = AgentDecisionDataPolicyFailure.Capture(AgentDecisionDataPolicyFailureStage.Publication,
            new AggregateException(Enumerable.Range(0, 100).Select(_ => new IOException("secret-path"))));
        Assert.Equal(16, bounded.ExceptionTypes.Length);
        Assert.True(bounded.Truncated);
        Assert.DoesNotContain(bounded.ExceptionTypes, type => type.Contains("secret", StringComparison.Ordinal));
        var combined = AgentDecisionDataPolicyFailure.Capture(AgentDecisionDataPolicyFailureStage.CacheInvalidation,
            new InvalidOperationException("secret-cache"), result.Failure);
        Assert.Equal(new[] { typeof(InvalidOperationException).FullName, typeof(NotSupportedException).FullName }, combined.ExceptionTypes);
    }

    private static AgentExecutionActivityWorkspaceIdentity Identity() => new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1));
    private static ReplaceAgentDecisionDataPolicyCommand Command(AgentExecutionActivityWorkspaceIdentity workspace) =>
        new(Guid.NewGuid(), new(workspace, Guid.NewGuid(), CatalogDataRevision.Initial), new());

    private class LegacyOwner : ISandboxWorkspaceCatalogStore {
        public int Calls { get; private set; }
        public Task<SandboxWorkspaceCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) => Unexpected<SandboxWorkspaceCatalog>();
        public Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(CancellationToken cancellationToken = default) => Unexpected<SandboxWorkspaceCatalogSnapshot>();
        public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(SandboxWorkspaceCatalog catalog, CancellationToken cancellationToken = default) => Unexpected<SandboxWorkspaceCatalog>();
        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            CancellationToken cancellationToken = default) => Unexpected<SandboxWorkspaceCatalog>();
        public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
            long expectedRevision, CancellationToken cancellationToken = default) => Unexpected<SandboxWorkspaceCatalog>();

        private Task<T> Unexpected<T>() {
            Calls++;
            throw new InvalidOperationException("The policy owner must not fall back to legacy persistence.");
        }
    }

    private sealed class ThrowingOwner : LegacyOwner, ISandboxWorkspaceCatalogStore {
        public Task<AgentDecisionDataPolicySaveOutcome> ReplaceAgentDecisionDataPolicyAsync(
            ReplaceAgentDecisionDataPolicyCommand command, Action validateCurrentContext, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Failure after entering an unknown custom owner cannot prove rejection.");
    }
}
