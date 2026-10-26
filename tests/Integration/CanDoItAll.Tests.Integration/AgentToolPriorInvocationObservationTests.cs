using System.Runtime.ExceptionServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentToolPriorInvocationObservationTests {
    [Theory]
    [InlineData(WorkspaceScopeKind.Organization)]
    [InlineData(WorkspaceScopeKind.Project)]
    [InlineData(WorkspaceScopeKind.Sandbox)]
    public Task Offline_workspace_exclusion_blocks_the_original_execution_writer_without_loading_or_changing_data(
        WorkspaceScopeKind kind) => RunAsync(async resources => {
        var originalScope = new WorkspaceScopeDescriptor(kind, kind == WorkspaceScopeKind.Sandbox ? null : Guid.NewGuid().ToString("N"));
        var fixture = resources.Own(await AgentToolAdmissionJournalFixture.CreateAsync(storageScope: originalScope));
        var maintenance = new FileSandboxWorkspaceMaintenanceExclusion(new(new PhysicalFileSystemPathPolicyFactory()));
        var before = SnapshotFiles(fixture.WorkspaceRoot);
        var held = resources.Own(await maintenance.AcquireAsync(fixture.WorkspaceRoot, originalScope));
        using var blocked = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.NewStore().UpdateExecutionRunDetailAsync(
            fixture.Session.ExecutionRunId, detail => detail, blocked.Token));
        await resources.ReleaseAsync(held);
        Assert.Equal(before, SnapshotFiles(fixture.WorkspaceRoot));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await fixture.NewStore().UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId, detail => detail, deadline.Token);
    });

    [Fact]
    public Task Offline_workspace_exclusion_does_not_decode_or_repair_catalog_and_refuses_missing_or_invalid_scopes()
        => RunAsync(async resources => {
            var fixture = resources.Own(await AgentToolAdmissionJournalFixture.CreateAsync());
            var originalScope = WorkspaceScopeDescriptor.Sandbox;
            await File.WriteAllTextAsync(Path.Combine(originalScope.ResolveDataRoot(fixture.WorkspaceRoot), "workspace.json"), "invalid retained catalog");
            var before = SnapshotFiles(fixture.WorkspaceRoot);
            var maintenance = new FileSandboxWorkspaceMaintenanceExclusion(new(new PhysicalFileSystemPathPolicyFactory()));
            var held = resources.Own(await maintenance.AcquireAsync(fixture.WorkspaceRoot, originalScope));
            await resources.ReleaseAsync(held);
            var missing = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("N"));
            await Assert.ThrowsAsync<PhysicalPathValidationException>(() => maintenance.AcquireAsync(fixture.WorkspaceRoot, missing).AsTask());
            await Assert.ThrowsAsync<ArgumentException>(() => maintenance.AcquireAsync(fixture.WorkspaceRoot,
                missing with { Key = "../other" }).AsTask());
            await Assert.ThrowsAsync<ArgumentException>(() => maintenance.AcquireAsync(fixture.WorkspaceRoot,
                new(WorkspaceScopeKind.Process, "unsupported")).AsTask());
            Assert.False(Directory.Exists(missing.ResolveDataRoot(fixture.WorkspaceRoot)));
            Assert.Equal(before, SnapshotFiles(fixture.WorkspaceRoot));
        });

    private static string[] SnapshotFiles(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(path => path + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))).ToArray();

    [Fact]
    public Task Dispatch_prefix_is_exact_readonly_and_refuses_foreign_replaced_or_missing_claims() => RunAsync(async scope => {
        var fixture = scope.Own(await AgentToolAdmissionJournalFixture.CreateAsync());
        var owner = fixture.NewJournal();
        var lease = scope.Own(await owner.AcquireRunAsync(fixture.Session, default));
        var facade = new AgentToolAdmissionVerifier();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => facade.RequirePriorInvocationsAsync(fixture.Session).AsTask());
        using var runtime = lease.Bind();
        await owner.BeginSegmentAsync(lease, Envelope(), null, default);
        var firstPayload = Payload(value: "original-created-project");
        var firstBatch = (await owner.AdmitBatchAsync(lease, firstPayload.Digest, Envelope(),
            [new("first", firstPayload, false)], default)).Batches[0];
        var first = await owner.ClaimInvocationAsync(lease, firstBatch.Id, "first", firstPayload, default);
        using (first.Bind()) {
            Assert.Empty((await facade.RequirePriorInvocationsAsync(fixture.Session)).Invocations);
            await owner.CompleteInvocationAsync(first, Envelope("{\"created\":1}"), AgentToolEffectState.Committed, default);
        }
        var priorPayload = Payload(value: "second-created-project");
        var currentPayload = Payload(value: "current-hierarchy");
        var futurePayload = Payload(value: "future-not-admitted-here");
        var secondBatch = (await owner.AdmitBatchAsync(lease, currentPayload.Digest, Envelope(),
            [new("prior", priorPayload, false), new("current", currentPayload, false), new("future", futurePayload, false)], default)).Batches[1];
        var prior = await owner.ClaimInvocationAsync(lease, secondBatch.Id, "prior", priorPayload, default);
        using (prior.Bind()) {
            await owner.CompleteInvocationAsync(prior, Envelope("{\"created\":2}"), AgentToolEffectState.Committed, default);
        }
        var current = await owner.ClaimInvocationAsync(lease, secondBatch.Id, "current", currentPayload, default);
        using var invocation = current.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => facade.RequirePriorResultInvocationsAsync(
            fixture.Session, current.Binding.IntentId, currentPayload).AsTask());
        var before = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!;
        var observed = await facade.RequirePriorInvocationsAsync(fixture.Session);
        Assert.Equal(before.ToolAdmission!.Session, observed.Session);
        Assert.Equal(current.Binding, observed.Current);
        Assert.Equal(before.Revision, observed.RunRevision);
        Assert.Equal(before.ToolAdmission.Revision, observed.JournalRevision);
        Assert.Collection(observed.Invocations,
            item => Assert.Equal(new AgentToolPriorInvocation(firstBatch.Id, before.ToolAdmission.Batches[0].Proposals[0]), item),
            item => Assert.Equal(new AgentToolPriorInvocation(secondBatch.Id, before.ToolAdmission.Batches[1].Proposals[0]), item));
        Assert.All(observed.Invocations, item => Assert.Equal(AgentToolProposalState.Completed, item.Proposal.State));
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await fixture.NewStore().GetExecutionRunAsync(before.Id)));
        var foreign = new AgentToolSessionReference(Guid.NewGuid(), fixture.Session.ChatSessionId, fixture.Session.AuthorityId);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => facade.RequirePriorInvocationsAsync(foreign).AsTask());
        var replaced = await owner.ClaimInvocationAsync(lease, secondBatch.Id, "current", currentPayload, default);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => facade.RequirePriorInvocationsAsync(fixture.Session).AsTask());
        using (replaced.Bind()) {
            Assert.Equal(observed.Invocations.ToArray(), (await facade.RequirePriorInvocationsAsync(fixture.Session)).Invocations.ToArray());
        }
        var unsupported = new UnsupportedVerifier();
        var refusal = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            ((IAgentToolAdmissionVerifier)unsupported).RequirePriorInvocationsAsync(fixture.Session).AsTask());
        Assert.Equal("tool-admission.prior-invocations-unavailable", refusal.Code);
        Assert.Equal(0, unsupported.OtherCalls);
        using (replaced.Bind()) {
            await owner.CompleteInvocationAsync(replaced, Envelope(), AgentToolEffectState.Committed, default);
            Assert.Equal(observed.Invocations.ToArray(), (await facade.RequirePriorInvocationsAsync(fixture.Session)).Invocations.ToArray());
        }
        var cached = await owner.ClaimInvocationAsync(lease, secondBatch.Id, "current", currentPayload, default);
        Assert.Equal(AgentToolProposalState.Completed, cached.Proposal.State);
        using (cached.Bind()) {
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => facade.RequirePriorInvocationsAsync(fixture.Session).AsTask());
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => cached.RequireCurrentIdentityAsync().AsTask());
        }
        var resultPrefix = await facade.RequirePriorResultInvocationsAsync(fixture.Session, current.Binding.IntentId, currentPayload);
        Assert.Equal(observed.Invocations.ToArray(), resultPrefix.Invocations.ToArray());
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => facade.RequirePriorResultInvocationsAsync(
            fixture.Session, current.Binding.IntentId, priorPayload).AsTask());
        Assert.Null(resultPrefix.PinnedEvidence);
    });

    [Theory]
    [InlineData(WorkspaceScopeKind.Organization, false)]
    [InlineData(WorkspaceScopeKind.Project, false)]
    [InlineData(WorkspaceScopeKind.Project, true)]
    [InlineData(WorkspaceScopeKind.Sandbox, false)]
    [InlineData(WorkspaceScopeKind.Sandbox, true)]
    public Task Owner_claim_validation_uses_original_store_and_refuses_a_replaced_claim_after_release(WorkspaceScopeKind kind,
        bool separateCatalogRoot) => RunAsync(async scope => {
        var profile = new AgentToolProfileBinding(Guid.NewGuid(), "held-catalog-claim", new(1));
        var organization = WorkspaceScopeDescriptor.Organization(profile.ProfileId.ToString("N"));
        var originalScope = kind switch {
            WorkspaceScopeKind.Organization => organization,
            WorkspaceScopeKind.Project => WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            WorkspaceScopeKind.Sandbox => WorkspaceScopeDescriptor.Sandbox,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var fixture = scope.Own(await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: profile, storageScope: originalScope));
        var catalogRoot = separateCatalogRoot
            ? scope.Own(await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: profile, storageScope: organization)).WorkspaceRoot
            : fixture.WorkspaceRoot;
        var catalogStore = new FileSandboxWorkspaceStore(catalogRoot, organization);
        await catalogStore.LoadCatalogSnapshotAsync();
        var owner = fixture.NewJournal();
        var lease = scope.Own(await owner.AcquireRunAsync(fixture.Session, default));
        using var bound = lease.Bind();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await owner.BeginSegmentAsync(lease, Envelope(), null, deadline.Token);
        var payload = Payload();
        var batch = (await owner.AdmitBatchAsync(lease, payload.Digest, Envelope(), [new("owner-effect", payload, false)], deadline.Token)).Batches[0];
        var claim = await owner.ClaimInvocationAsync(lease, batch.Id, "owner-effect", payload, deadline.Token);
        using var invocation = claim.Bind();
        await claim.RequireCurrentIdentityAsync(deadline.Token);
        await using (var catalog = await catalogStore.AcquireAgentReadLeaseAsync(fixture.Agent.Id, deadline.Token)) {
            await using var publication = await claim.AcquirePublicationAsync(catalog, deadline.Token);
            await publication.RequireCurrentAsync(deadline.Token);
            if (kind == WorkspaceScopeKind.Organization) {
                Assert.IsType<AgentExecutionPublicationRecovery.SharedCatalog>(publication.CaptureRecovery());
            } else {
                var recovery = Assert.IsType<AgentExecutionPublicationRecovery.Workspace>(publication.CaptureRecovery());
                Assert.Equal(Path.GetFullPath(fixture.WorkspaceRoot), recovery.RootPath);
                Assert.Equal(originalScope, recovery.Scope);
                if (separateCatalogRoot) {
                    Assert.NotEqual(Path.GetFullPath(catalogRoot), recovery.RootPath);
                }
            }
            using var blocked = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.NewStore().UpdateExecutionRunDetailAsync(
                fixture.Session.ExecutionRunId, detail => detail, blocked.Token));
            await publication.RequireCurrentAsync(deadline.Token);
            await publication.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => publication.CaptureRecovery());
        }
        var replaced = await owner.ClaimInvocationAsync(lease, batch.Id, "owner-effect", payload, deadline.Token);
        await using (var catalog = await catalogStore.AcquireAgentReadLeaseAsync(fixture.Agent.Id, deadline.Token)) {
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => claim.AcquirePublicationAsync(catalog, deadline.Token).AsTask());
            using (replaced.Bind()) {
                await using var publication = await replaced.AcquirePublicationAsync(catalog, deadline.Token);
                await publication.RequireCurrentAsync(deadline.Token);
            }
        }
    });

    [Fact]
    public Task Interrupted_receipt_prefix_retains_pinned_original_evidence_and_rechecks_read_permission_without_dispatch() => RunAsync(async scope => {
        var seed = scope.Own(await AgentToolAdmissionJournalFixture.CreateAsync());
        var run = EffectsRun(seed, cancelled: false);
        var fixture = scope.Own(await AgentToolAdmissionJournalFixture.RestoreRetainedAsync(seed.Agent, seed.Provider, seed.Profile, run));
        var owner = fixture.NewJournal();
        var lease = scope.Own(await owner.AcquireInterruptedReconciliationAsync(fixture.Session, default));
        using var runtime = lease.Bind();
        var pinned = lease.RequirePinnedSnapshot(owner);
        var readable = true;
        var reads = 0;
        var binding = Binding(run.ToolAdmission!);
        var claim = new AgentToolReceiptReconciliationClaim(owner, lease, binding, currentRead: (observation, _) => {
            reads++;
            Assert.Equal(pinned.Admission.Run.ToolAdmission!.Session, observation.Session);
            return new(readable);
        }, position: new(binding.BatchId, binding.IntentId, 0, 1));
        var forgedDispatch = new AgentToolInvocationClaim(owner, lease, binding, run.ToolAdmission!.Batches[0].Proposals[1],
            run.ToolAdmission.Batches[0].Proposals[1].DispatchClaimId!.Value, pinned.Evidence.JournalRevision);
        Assert.Throws<AgentToolAdmissionException>(() => forgedDispatch.Bind());
        var original = await claim.RequirePriorInvocationsAsync();
        Assert.Equal(pinned.Admission.Run.Revision, original.RunRevision);
        Assert.Equal(pinned.Admission.Run.ToolAdmission!.Revision, original.JournalRevision);
        Assert.Same(pinned.Evidence, original.PinnedEvidence);
        Assert.Equal(binding, original.Current);
        Assert.Equal(run.ToolAdmission!.Batches[0].Proposals[0], Assert.Single(original.Invocations).Proposal);
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Store).UpdateExecutionRunDetailAsync(run.Id, detail => detail with {
            Run = detail.Run with { State = ExecutionState.Completed, Outcome = RunOutcome.Succeeded, Revision = detail.Run.Revision + 1 }
        });
        var current = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!;
        Assert.True(current.Revision > original.RunRevision);
        var retained = await claim.RequirePriorInvocationsAsync();
        Assert.Equal(original.RunRevision, retained.RunRevision);
        Assert.Equal(original.JournalRevision, retained.JournalRevision);
        Assert.Equal(original.Invocations.ToArray(), retained.Invocations.ToArray());
        Assert.Same(original.PinnedEvidence, retained.PinnedEvidence);
        Assert.Equal(2, reads);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => new AgentToolAdmissionVerifier()
            .RequirePriorInvocationsAsync(fixture.Session).AsTask());
        readable = false;
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => claim.RequirePriorInvocationsAsync().AsTask());
        var wrong = new AgentToolReceiptReconciliationClaim(owner, lease,
            binding with { Digest = Payload(value: "different-original").Digest }, currentRead: (_, _) => throw new InvalidOperationException());
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => wrong.RequirePriorInvocationsAsync().AsTask());
        Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(await fixture.NewStore().GetExecutionRunAsync(run.Id)));
    });

    [Fact]
    public Task Cancelled_receipt_prefix_remains_current_session_read_evidence_and_refuses_released_or_foreign_positions() => RunAsync(async scope => {
        var seed = scope.Own(await AgentToolAdmissionJournalFixture.CreateAsync());
        var run = EffectsRun(seed, cancelled: true);
        var fixture = scope.Own(await AgentToolAdmissionJournalFixture.RestoreRetainedAsync(seed.Agent, seed.Provider, seed.Profile, run));
        var owner = fixture.NewJournal();
        var lease = scope.Own(await owner.AcquireCancelledReconciliationAsync(fixture.Session, default));
        var claim = new AgentToolReceiptReconciliationClaim(owner, lease, Binding(run.ToolAdmission!));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => claim.RequirePriorInvocationsAsync().AsTask());
        using (lease.Bind()) {
            var before = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!;
            var observed = await claim.RequirePriorInvocationsAsync();
            Assert.Null(observed.PinnedEvidence);
            Assert.Equal(before.Revision, observed.RunRevision);
            Assert.Equal(before.ToolAdmission!.Session, observed.Session);
            Assert.Equal(before.ToolAdmission.Batches[0].Proposals[0], Assert.Single(observed.Invocations).Proposal);
            var foreign = new AgentToolReceiptReconciliationClaim(owner, lease,
                observed.Current with { BatchId = new(Guid.NewGuid()) });
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => foreign.RequirePriorInvocationsAsync().AsTask());
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => new AgentToolAdmissionVerifier()
                .RequirePriorInvocationsAsync(fixture.Session).AsTask());
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => claim.RequirePriorInvocationsAsync(cancelled.Token).AsTask());
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await fixture.NewStore().GetExecutionRunAsync(run.Id)));
        }
        await scope.ReleaseAsync(lease);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => claim.RequirePriorInvocationsAsync().AsTask());
    });

    private static ExecutionRunRecord EffectsRun(AgentToolAdmissionJournalFixture fixture, bool cancelled) {
        var payload = Payload();
        var originalLease = Guid.NewGuid();
        var prior = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, "prior-created", payload, false,
            AgentToolProposalState.Completed, ExecutionApprovalStatus.Approved, payload.Digest,
            DispatchClaimId: Guid.NewGuid(), Result: Envelope("{\"created\":true}"), EffectState: AgentToolEffectState.Committed);
        var current = new AgentToolProposalRecord(new(Guid.NewGuid()), 1, "original-hierarchy", payload, false,
            AgentToolProposalState.Executing, ExecutionApprovalStatus.Approved, payload.Digest, DispatchClaimId: Guid.NewGuid());
        var future = new AgentToolProposalRecord(new(Guid.NewGuid()), 2, "later-create", payload, false,
            AgentToolProposalState.Prepared, ExecutionApprovalStatus.Approved, payload.Digest);
        var journal = fixture.Detail.Run.ToolAdmission! with {
            SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion, Support = AgentToolAdmissionSupport.EffectJournalOnly,
            ActiveDispatchLeaseId = originalLease,
            Segments = [new(Guid.NewGuid(), 0, 0, null, PendingApprovals: [],
                CheckpointKind: AgentToolCheckpointKind.EffectsOnly, OriginDispatchLeaseId: originalLease)],
            Batches = [new(new(Guid.NewGuid()), 0, payload.Digest, Envelope(), [prior, current, future])]
        };
        journal.Validate();
        return fixture.Detail.Run with { ToolAdmission = journal,
            State = cancelled ? ExecutionState.Failed : ExecutionState.Preparing,
            Outcome = cancelled ? RunOutcome.Cancelled : null };
    }

    private static AgentToolApprovalBinding Binding(AgentToolJournalRecord journal) {
        var batch = journal.Batches[0];
        var proposal = batch.Proposals[1];
        return new(batch.Id, proposal.IntentId, proposal.Payload.SemanticVersion, proposal.Payload.Digest);
    }
    private static AgentToolPreparedPayload Payload(string value = "alpha") => AgentToolAdmissionJournalFixture.Payload(value: value);
    private static AgentToolProtocolEnvelope Envelope(string json = "{}") => AgentToolAdmissionJournalFixture.Envelope(json);

    private sealed class UnsupportedVerifier : IAgentToolAdmissionVerifier {
        internal int OtherCalls { get; private set; }
        public ValueTask<AgentToolSessionAdmission> RequireSessionAsync(AgentToolSessionReference reference, CancellationToken cancellationToken = default) {
            OtherCalls++;
            throw new InvalidOperationException();
        }
        public ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(AgentToolSessionReference session, string toolName,
            AgentToolSemanticDigest digest, CancellationToken cancellationToken = default) {
            OtherCalls++;
            throw new InvalidOperationException();
        }
    }

    private static async Task RunAsync(Func<OwnedResources, Task> body) {
        var scope = new OwnedResources();
        Exception? primary = null;
        try {
            await body(scope);
        } catch (Exception failure) {
            primary = failure;
        }
        var cleanup = new List<Exception>();
        for (var index = scope.Resources.Count - 1; index >= 0; index--) {
            try {
                await scope.Resources[index].DisposeAsync();
            } catch (Exception failure) {
                cleanup.Add(failure);
            }
        }
        if (cleanup.Count != 0) {
            throw new AggregateException(primary is null ? cleanup : [primary, .. cleanup]);
        }
        if (primary is not null) {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }

    private sealed class OwnedResources {
        internal List<IAsyncDisposable> Resources { get; } = [];
        internal T Own<T>(T resource) where T : IAsyncDisposable {
            Resources.Add(resource);
            return resource;
        }
        internal async Task ReleaseAsync(IAsyncDisposable resource) {
            Assert.True(Resources.Remove(resource));
            await resource.DisposeAsync();
        }
    }
}
