using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceStore : IAgentCatalogReadLeaseStore {
    private const int MaximumSelectedLeaseAgents = 2;

    public async Task<IAgentPlanningPublicationSnapshotLease> AcquirePlanningAuthoritySnapshotAsync(
        AgentPlanningPublicationExpectation expected, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        if (layout.Scope != expected.Scope) {
            throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged);
        }
        var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout,
            runLeaseWriter, cancellationToken).ConfigureAwait(false);
        try {
            await EnsureCatalogReadCoreAsync(operation, cancellationToken).ConfigureAwait(false);
            _ = await LoadNormalizedCatalogCoreAsync(operation, cancellationToken).ConfigureAwait(false);
            var saved = await executionSliceStore.LoadRunAdmissionSnapshotAsync(expected.Session.Reference.ExecutionRunId,
                cancellationToken).ConfigureAwait(false);
            FileAgentPlanningPublicationSnapshotLease.RequireOriginal(expected, layout.Scope, saved);
            var original = saved!;
            var witness = await FileSandboxWorkspaceMutationWitness.CaptureAsync(layout,
                PendingCommitJournalPaths.Append(layout.RunPath(original.Run.Id)).Append(layout.SessionPath(original.ChatSession!.Id)),
                [layout.RunRoot(original.Run.Id)], PendingCommitJournalPaths, physicalPathPolicyFactory, cancellationToken,
                SharedPublicationPaths).ConfigureAwait(false);
            _ = await operation.AcquireAsync(FileSandboxWorkspaceWriteSet.Run(original.Run, original.Run, readsAgent: true),
                witness, cancellationToken).ConfigureAwait(false);
            var current = await executionSliceStore.LoadRunAdmissionSnapshotAsync(original.Run.Id, cancellationToken).ConfigureAwait(false);
            FileAgentPlanningPublicationSnapshotLease.RequireOriginal(expected, layout.Scope, current);
            var catalog = await LoadCatalogCoreAsync(cancellationToken).ConfigureAwait(false);
            var agent = catalog.Agents.SingleOrDefault(item => item.Id == expected.Session.AgentId)
                ?? throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged);
            var selectedCapabilities = agent.Capabilities.Select(item => item.CapabilityId).ToHashSet();
            var lease = new FileAgentPlanningPublicationSnapshotLease(operation, expected, catalog.CatalogDataRevision, agent,
                current!, catalog.Capabilities.Where(item => selectedCapabilities.Contains(item.Id)));
            await operation.ReleaseWorkspaceForAuthoritySnapshotAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return lease;
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            await operation.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default) {
        if (agentId == Guid.Empty) {
            throw new ArgumentException("An agent identifier is required for a catalog read lease.", nameof(agentId));
        }
        return AcquireAgentsReadLeaseAsync([agentId], cancellationToken);
    }

    public async Task<IAgentCatalogReadLease> AcquireAgentsReadLeaseAsync(IReadOnlyList<Guid> agentIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(agentIds);
        if (agentIds.Count is < 1 or > MaximumSelectedLeaseAgents || agentIds.Contains(Guid.Empty) || agentIds.Distinct().Count() != agentIds.Count) {
            throw new ArgumentException("A catalog read lease requires one or two distinct nonempty Agent identifiers.", nameof(agentIds));
        }
        var selectedIds = agentIds.ToImmutableArray();
        var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout,
            runLeaseWriter, cancellationToken).ConfigureAwait(false);
        try {
            await EnsureCatalogReadCoreAsync(operation, cancellationToken).ConfigureAwait(false);
            var catalog = await LoadNormalizedCatalogCoreAsync(operation, cancellationToken).ConfigureAwait(false);
            var agents = selectedIds.Select(id => catalog.Agents.SingleOrDefault(item => item.Id == id))
                .OfType<AgentDefinition>().Select(agent => agent with {
                    Permissions = agent.Permissions with { AllowedSecrets = agent.Permissions.NormalizedAllowedSecrets.ToImmutableArray() },
                    Capabilities = agent.Capabilities.ToImmutableArray(),
                    Tags = agent.Tags.ToImmutableArray()
                }).ToImmutableArray();
            return new AgentCatalogReadLease(operation, layout.Scope, catalog.CatalogDataRevision,
                agents.SingleOrDefault(agent => agent.Id == selectedIds[0]), agents,
                catalog.Capabilities.Select(item => item with { Tags = item.Tags.ToImmutableArray() }).ToImmutableArray(), this);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            await operation.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class ExecutionAdmissionReadLease(IAsyncDisposable? publication,
        ExecutionRunAdmissionSnapshot snapshot, AgentExecutionPublicationRecovery recovery,
        Action requireCatalog) : IAgentExecutionAdmissionReadLease {
        private int disposed;

        public ExecutionRunAdmissionSnapshot Snapshot {
            get {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                requireCatalog();
                return snapshot;
            }
        }

        public AgentExecutionPublicationRecovery Recovery {
            get {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
                requireCatalog();
                return recovery;
            }
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && publication is not null) {
                await publication.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class AgentCatalogReadLease(IAsyncDisposable workspaceLock,
        WorkspaceScopeDescriptor scope, CatalogDataRevision revision, AgentDefinition? agent,
        ImmutableArray<AgentDefinition> agents, ImmutableArray<CapabilityCatalogItem> capabilities,
        FileSandboxWorkspaceStore owner) : IAgentCatalogReadLease {
        private IAsyncDisposable? heldLock = workspaceLock;

        public WorkspaceScopeDescriptor Scope {
            get {
                RequireActive();
                return scope;
            }
        }

        public CatalogDataRevision Revision {
            get {
                RequireActive();
                return revision;
            }
        }

        public AgentDefinition? Agent {
            get {
                RequireActive();
                return agent;
            }
        }

        public ImmutableArray<AgentDefinition> Agents {
            get {
                RequireActive();
                return agents;
            }
        }

        public ImmutableArray<CapabilityCatalogItem> Capabilities {
            get {
                RequireActive();
                return capabilities;
            }
        }

        public async ValueTask DisposeAsync() {
            var acquired = Interlocked.Exchange(ref heldLock, null);
            if (acquired is null) {
                return;
            }
            await acquired.DisposeAsync().ConfigureAwait(false);
        }

        public async Task<IAgentExecutionAdmissionReadLease> AcquireExecutionAdmissionReadLeaseAsync(
            ISandboxWorkspaceExecutionRunStore executions, Guid executionRunId, CancellationToken cancellationToken = default) {
            RequireActive();
            if (scope.Kind != WorkspaceScopeKind.Organization || executions is not FileSandboxWorkspaceStore original || executionRunId == Guid.Empty) {
                throw new InvalidOperationException("A canonical organization catalog and an original file execution store are required.");
            }
            var paths = owner.physicalPathPolicyFactory.Create(owner.layout.RootPath);
            var shared = paths.PathComparer.Equals(Path.GetFullPath(owner.layout.WorkspaceLockPath),
                Path.GetFullPath(original.layout.WorkspaceLockPath));
            if (!shared && original.layout.Scope.Kind is not (WorkspaceScopeKind.Project or WorkspaceScopeKind.Sandbox)) {
                throw new InvalidOperationException("Execution publication must reuse the organization lock or follow it with a project or sandbox lock.");
            }
            FileSandboxWorkspacePublicationScope? publication = null;
            try {
                if (!shared) {
                    publication = await FileSandboxWorkspacePublicationScope.EnterAsync(original.gate, original.crossProcessLock,
                        original.layout, original.runLeaseWriter, cancellationToken).ConfigureAwait(false);
                    await original.EnsureExecutionSummaryReadCoreAsync(publication, cancellationToken).ConfigureAwait(false);
                }
                var snapshot = await original.executionSliceStore.LoadRunAdmissionSnapshotAsync(executionRunId, cancellationToken)
                    ?? throw new InvalidOperationException("The original execution is absent from its publication store.");
                RequireActive();
                cancellationToken.ThrowIfCancellationRequested();
                AgentExecutionPublicationRecovery recovery = shared
                    ? new AgentExecutionPublicationRecovery.SharedCatalog()
                    : new AgentExecutionPublicationRecovery.Workspace(Path.GetFullPath(original.layout.RootPath), original.layout.Scope);
                return new ExecutionAdmissionReadLease(publication, snapshot, recovery, RequireActive);
            } catch (Exception primary) {
                if (publication is not null) {
                    publication.RetainFailure(primary);
                    await publication.DisposeAsync().ConfigureAwait(false);
                }
                throw;
            }
        }

        private void RequireActive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref heldLock) is null, this);
    }
}
