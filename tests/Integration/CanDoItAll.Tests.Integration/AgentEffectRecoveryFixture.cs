using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class AgentEffectRecoveryFixture : IAsyncDisposable {
    private readonly AgentExecutionPreparationCache cache = new(AgentExecutionPreparationCachePolicy.Default);

    private AgentEffectRecoveryFixture(AgentToolAdmissionJournalFixture fixture, Action<ExistingRunDetailCommitStage>? fault, WorkspaceScopeDescriptor? activityScope, AgentExecutionActivityCoordinator? coordinator, bool failLeaseRelease,
        Func<IAgentExecutionActivityCoordinator, IAgentExecutionActivityCoordinator>? activityDecorator) {
        Fixture = fixture;
        Store = fixture.NewStore(fault);
        ISandboxWorkspaceStore journalStore = Store;
        if (failLeaseRelease) {
            var decorated = DispatchProxy.Create<IAgentEffectRecoveryLeaseFailureStore, AgentEffectRecoveryLeaseFailureStore>();
            var forwarding = (AgentEffectRecoveryLeaseFailureStore)(object)decorated;
            forwarding.Target = Store;
            forwarding.Released = () => LeaseReleaseFaults++;
            journalStore = decorated;
        }
        Journal = new(journalStore, fixture.Profile);
        Resolver = new(fixture.Detail.Run);
        Runtime = new();
        Receipts = new();
        Coordinator = coordinator ?? new(new PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity>(
            PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
        Identity = new(fixture.Profile.ProfileId, activityScope ?? WorkspaceScopeDescriptor.Sandbox, fixture.Profile.Generation);
        Service = new(Store, new ZipAgentPackageService(fixture.WorkspaceRoot, WorkspaceScopeDescriptor.Sandbox),
            Runtime, Runtime, Runtime, Runtime, new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()),
            NullLogger<AgentFrameworkWorkspaceService>.Instance, activityDecorator?.Invoke(Coordinator) ?? Coordinator, Identity, cache,
            new FixedAgentExecutionProfileGenerationSource(fixture.Profile.Generation), new NoProcessLeases(),
            new ExternalTargetPathRegistryFactory(), toolAdmissionJournal: Journal, executionAuthorityResolver: Resolver,
            receiptReconciliationProviders: [Receipts]);
    }

    internal AgentToolAdmissionJournalFixture Fixture { get; }
    internal ExecutionRunRecord Original => Fixture.Detail.Run;
    internal FileSandboxWorkspaceStore Store { get; }
    internal AgentToolAdmissionJournal Journal { get; }
    internal int LeaseReleaseFaults { get; private set; }
    internal AuthorityResolver Resolver { get; }
    internal ForbiddenRuntime Runtime { get; }
    internal ReceiptReader Receipts { get; }
    internal AgentExecutionActivityCoordinator Coordinator { get; }
    internal AgentExecutionActivityWorkspaceIdentity Identity { get; }
    internal AgentFrameworkWorkspaceService Service { get; }

    internal static async Task<AgentEffectRecoveryFixture> CreateAsync(Func<ExecutionRunRecord, ExecutionRunRecord>? configure = null,
        Action<ExistingRunDetailCommitStage>? fault = null, AgentToolProfileBinding? profileBinding = null, WorkspaceScopeDescriptor? activityScope = null, AgentExecutionActivityCoordinator? coordinator = null, bool failLeaseRelease = false,
        Func<IAgentExecutionActivityCoordinator, IAgentExecutionActivityCoordinator>? activityDecorator = null) {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: profileBinding);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var originLease = Guid.NewGuid();
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, "original-call", payload, false,
            AgentToolProposalState.Executing, ExecutionApprovalStatus.Approved, payload.Digest, DispatchClaimId: Guid.NewGuid());
        var run = source.Detail.Run with { ToolAdmission = source.Detail.Run.ToolAdmission! with {
            SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion, Support = AgentToolAdmissionSupport.EffectJournalOnly,
            ActiveDispatchLeaseId = originLease,
            Segments = [new(Guid.NewGuid(), 0, 0, null, PendingApprovals: [], CheckpointKind: AgentToolCheckpointKind.EffectsOnly,
                OriginDispatchLeaseId: originLease)],
            Batches = [new(new(Guid.NewGuid()), 0, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(), [proposal])]
        } };
        run = configure?.Invoke(run) ?? run;
        run.ToolAdmission!.Validate();
        var fixture = await AgentToolAdmissionJournalFixture.RestoreRetainedAsync(source.Agent, source.Provider, source.Profile, run);
        try {
            return new(fixture, fault, activityScope, coordinator, failLeaseRelease, activityDecorator);
        } catch {
            await fixture.DisposeAsync();
            throw;
        }
    }

    internal Task<ExecutionRunResult> RecoverAsync(AgentExecutionOperationId? operation = null, CancellationToken cancellationToken = default)
        => Service.RecoverExecutionRunAsync(Original.Id, operation ?? AgentExecutionOperationId.New(), cancellationToken);

    public async ValueTask DisposeAsync() {
        try {
            Service.Dispose();
        } finally {
            try {
                cache.Dispose();
            } finally {
                await Fixture.DisposeAsync();
            }
        }
    }

    internal sealed class AuthorityResolver(ExecutionRunRecord original) : IAgentExecutionAuthorityResolver {
        internal bool ReadAllowed { get; set; } = true;
        internal bool WriteAllowed { get; set; } = true;
        internal Func<AgentExecutionAuthorityRecord, AgentExecutionAuthorityRecord>? Change { get; set; }
        internal int Calls { get; private set; }
        internal List<AgentExecutionAuthorityId> ObservedIds { get; } = [];
        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            var admitted = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(original.MetadataJson)!;
            Assert.Equal(admitted.AuthorityId, request.Revalidation!.Authority.AuthorityId);
            Assert.Equal(AgentTurnContextMetadata.TryReadTurnContextReference(original.MetadataJson), request.Revalidation.Source);
            var current = new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), admitted.AgentId,
                admitted.DatabaseProfileId, admitted.DatabaseProfileGeneration, admitted.WorkspaceScope,
                ReadAllowed, ReadAllowed && WriteAllowed, "current-read-policy", "current-read-observation", DateTimeOffset.UtcNow,
                allowedOperations: admitted.AllowedOperations.ToArray(), allowedCapabilityKeys: admitted.AllowedCapabilityKeys.ToArray(),
                allowedExternalTargetAliases: admitted.WritableExternalTargetAliases.ToArray(),
                readOnlyExternalTargetAliases: admitted.ReadOnlyExternalTargetAliases.ToArray(),
                schemaVersion: admitted.EffectiveSchemaVersion, sourceProjectLifetime: admitted.SourceProjectLifetime);
            current = Change?.Invoke(current) ?? current;
            ObservedIds.Add(current.AuthorityId);
            return new(current);
        }
    }

    internal sealed class ReceiptReader : IAgentToolReceiptReconciliationProvider {
        internal int Calls { get; private set; }
        internal Func<AgentToolReceiptReconciliationClaim, CancellationToken, ValueTask<AgentToolReceiptObservation>>? Observe { get; set; }
        public bool Supports(string toolName) => toolName == AgentToolAdmissionJournalFixture.Payload().ToolName;
        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            Calls++;
            await claim.RequireAsync(cancellationToken);
            return Observe is null ? new(new("fixture-owner", "private-source"),
                AgentToolAdmissionJournalFixture.Envelope("{\"accepted\":\"private-owner-result\"}")) : await Observe(claim, cancellationToken);
        }
    }

    internal sealed class NoProcessLeases : IWorkspaceExecutionRunProcessLeaseCleaner {
        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
            => Task.FromResult(WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
    }

    internal sealed class ForbiddenRuntime : IAgentExecutionRuntime, IAgentContinuationRuntime,
        IProviderDiagnosticsRuntime, IProviderModelAdministrationRuntime {
        internal int Calls { get; private set; }
        private Exception Reached() {
            Calls++;
            return new InvalidOperationException("Effect reconciliation reached a forbidden provider or SDK operation.");
        }
        public Task<AgentRuntimeResponse> ExecuteAsync(AgentRuntimeExecutionRequest request, CancellationToken cancellationToken = default) => throw Reached();
        public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default) => throw Reached();
        public Task<ProviderHealthResult> TestHealthAsync(ProviderProfile provider, CancellationToken cancellationToken = default) => throw Reached();
        public Task<ProviderTestChatResult> RunProbeAsync(ProviderProfile provider, ProviderTestChatRequest request, CancellationToken cancellationToken = default) => throw Reached();
        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw Reached();
    }
}

public interface IAgentEffectRecoveryLeaseFailureStore : ISandboxWorkspaceStore, ISandboxWorkspaceExecutionRunStore,
    ISandboxWorkspaceExecutionRunMutationStore, ISandboxWorkspaceExecutionRunLeaseStore {
}

public class AgentEffectRecoveryLeaseFailureStore : DispatchProxy {
    internal FileSandboxWorkspaceStore Target { get; set; } = null!;
    internal Action Released { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        try {
            var result = method!.Invoke(Target, args);
            return method.Name == nameof(ISandboxWorkspaceExecutionRunLeaseStore.AcquireToolDispatchLeaseAsync)
                ? WrapAsync((ValueTask<IAsyncDisposable>)result!) : result;
        } catch (TargetInvocationException exception) when (exception.InnerException is not null) {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
    private async ValueTask<IAsyncDisposable> WrapAsync(ValueTask<IAsyncDisposable> pending)
        => new AgentEffectRecoveryReleaseFailure(await pending, Released);
}

internal sealed class AgentEffectRecoveryReleaseFailure(IAsyncDisposable actual, Action released) : IAsyncDisposable {
    public async ValueTask DisposeAsync() {
        await actual.DisposeAsync();
        released();
        throw new TimeoutException("Injected failure after actual lease release.");
    }
}
