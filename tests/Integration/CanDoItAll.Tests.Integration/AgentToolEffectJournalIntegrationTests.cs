using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed partial class AgentToolEffectJournalIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Readonly_lease_rejects_every_dispatch_batch_provider_and_approval_entrypoint(bool cancelled) {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = EffectsRun(source);
        if (cancelled) {
            run = run with { State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled };
        }
        await using var fixture = await RestoreAsync(source, run);
        var journal = fixture.NewJournal();
        await using var lease = cancelled
            ? await journal.AcquireCancelledReconciliationAsync(fixture.Session, default)
            : await journal.AcquireInterruptedReconciliationAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var before = JsonSerializer.Serialize(await fixture.NewStore().GetExecutionRunDetailAsync(run.Id));
        var saved = run.ToolAdmission!;
        var batch = saved.Batches[0];
        var proposal = batch.Proposals[0];
        var binding = Binding(saved);
        var envelope = AgentToolAdmissionJournalFixture.Envelope();
        var forgedCompleted = new AgentToolInvocationClaim(journal, lease, binding,
            proposal with { State = AgentToolProposalState.Completed, Result = envelope }, Guid.NewGuid(), saved.Revision);
        Func<Task>[] operations = [
            () => journal.ReadAsync(lease, default),
            () => journal.RequireSessionAsync(fixture.Session).AsTask(),
            () => journal.RequireSessionObservationAsync(fixture.Session).AsTask(),
            () => journal.RequireInvocationAsync(fixture.Session, proposal.Payload.ToolName, proposal.Payload.Digest).AsTask(),
            () => journal.BeginSegmentAsync(lease, envelope, null, default),
            () => journal.BeginEffectSegmentAsync(lease, default),
            () => journal.AdmitBatchAsync(lease, proposal.Payload.Digest, envelope, [new("new-call", proposal.Payload, false)], default),
            () => journal.ClaimInvocationAsync(lease, batch.Id, proposal.CallId, proposal.Payload, default),
            () => journal.BeginProviderDispatchAsync(lease, saved.Segments[0].Id, proposal.Payload.Digest, proposal.Payload.Digest, [], default),
            () => journal.CompleteProviderDispatchAsync(lease, new(Guid.NewGuid()), proposal.Payload.Digest, envelope, [], default),
            () => journal.SaveApprovalCheckpointAsync(lease, saved.Segments[0].Id, envelope, "{}", [], default),
            () => journal.SaveEffectApprovalsAsync(lease, saved.Segments[0].Id, [], default),
            () => journal.CompleteInvocationAsync(forgedCompleted, envelope, AgentToolEffectState.Committed, default),
            () => journal.PreserveUncertainInvocationAsync(forgedCompleted, default)
        ];
        foreach (var operation in operations) {
            await Assert.ThrowsAsync<AgentToolAdmissionException>(operation);
        }
        Assert.Throws<AgentToolAdmissionException>(() => forgedCompleted.Bind());
        if (!cancelled) {
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.ReconcileCancelledAsync(lease, [], true, default));
        }
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.NewStore().GetExecutionRunDetailAsync(run.Id)));
        Assert.Equal(proposal.DispatchClaimId, (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!.Batches[0].Proposals[0].DispatchClaimId);
    }

    [Fact]
    public async Task Interrupted_reconciliation_requires_exact_nonterminal_effect_session() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var valid = EffectsRun(source);
        ExecutionRunRecord[] refused = [
            source.Detail.Run,
            valid with { State = ExecutionState.Idle },
            valid with { State = ExecutionState.Completed },
            valid with { Outcome = RunOutcome.Succeeded },
            valid with { Outcome = RunOutcome.Cancelled },
            valid with { State = ExecutionState.Running, Outcome = RunOutcome.Failed },
            source.Detail.Run with { ToolAdmission = source.Detail.Run.ToolAdmission! with {
                SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion
            } }
        ];
        foreach (var run in refused) {
            await using var rejected = await RestoreAsync(source, run);
            var before = JsonSerializer.Serialize(await rejected.NewStore().GetExecutionRunAsync(run.Id));
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
                rejected.NewJournal().AcquireInterruptedReconciliationAsync(rejected.Session, default).AsTask());
            Assert.Equal(before, JsonSerializer.Serialize(await rejected.NewStore().GetExecutionRunAsync(run.Id)));
            await using var available = await ((ISandboxWorkspaceExecutionRunLeaseStore)rejected.NewStore())
                .AcquireToolDispatchLeaseAsync(run.Id, default);
        }

        foreach (var state in new[] { ExecutionState.Preparing, ExecutionState.Running, ExecutionState.WaitingOnTool, ExecutionState.Persisting, ExecutionState.Failed }) {
            await using var accepted = await RestoreAsync(source, valid with {
                State = state, Outcome = state == ExecutionState.Failed ? RunOutcome.Failed : null
            });
            await using var admitted = await accepted.NewJournal().AcquireInterruptedReconciliationAsync(accepted.Session, default);
            Assert.Equal(state, (await accepted.NewStore().GetExecutionRunAsync(valid.Id))!.State);
        }

        await using var fixture = await RestoreAsync(source, valid);
        var owner = fixture.NewJournal();
        var wrongReference = new AgentToolSessionReference(fixture.Session.ExecutionRunId, fixture.Session.ChatSessionId,
            AgentExecutionAuthorityId.Create());
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.AcquireInterruptedReconciliationAsync(wrongReference, default).AsTask());
        var foreign = new AgentToolAdmissionJournal(fixture.NewStore(), new(Guid.NewGuid(), "foreign-profile", new(1)));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => foreign.AcquireInterruptedReconciliationAsync(fixture.Session, default).AsTask());
        await using var lease = await owner.AcquireInterruptedReconciliationAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var binding = Binding(valid.ToolAdmission!);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => new AgentToolReceiptReconciliationClaim(owner, lease, binding).RequireAsync().AsTask());
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() =>
            new AgentToolReceiptReconciliationClaim(owner, lease, binding, currentRead: (_, _) => new(false)).RequireAsync().AsTask());
        var unavailable = await Assert.ThrowsAsync<AgentToolReceiptReadUnavailableException>(() => new AgentToolReceiptReconciliationClaim(owner, lease, binding,
            currentRead: (_, _) => throw new IOException("Injected current policy read failure.")).RequireAsync().AsTask());
        Assert.Equal("Injected current policy read failure.", Assert.IsType<IOException>(unavailable.InnerException).Message);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AgentToolReceiptReconciliationClaim(owner, lease, binding,
            currentRead: (_, _) => new(true)).RequireAsync(cancelled.Token).AsTask());
        var receiptAdmission = await new AgentToolReceiptReconciliationClaim(owner, lease, binding,
            currentRead: (_, _) => new(true)).RequireAsync();
        Assert.Equal(valid.ToolAdmission!.Batches[0].Proposals[0].Payload, receiptAdmission.Payload);
        Assert.NotNull(receiptAdmission.PinnedEvidence);
        Assert.DoesNotContain(nameof(AgentToolReceiptReconciliationAdmission.PinnedEvidence), JsonSerializer.Serialize(receiptAdmission));
        Assert.Null(new AgentToolReceiptReconciliationAdmission(receiptAdmission.Session, receiptAdmission.Binding, receiptAdmission.Payload).PinnedEvidence);
    }

    [Fact]
    public async Task Independent_journals_serialize_reconciliation_and_preserve_original_intent() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = EffectsRun(source);
        await using var fixture = await RestoreAsync(source, run);
        var observedStore = DispatchProxy.Create<IObservedStore, ObservedStoreProxy>();
        var observer = (ObservedStoreProxy)(object)observedStore;
        observer.Store = fixture.NewStore();
        var journal = new AgentToolAdmissionJournal(observedStore, fixture.Profile);
        var other = fixture.NewJournal(fixture.NewStore());
        var lease = await journal.AcquireInterruptedReconciliationAsync(fixture.Session, default);
        try {
            Assert.Equal(1, observer.Updates);
            Assert.Equal(0, observer.RunReads);
            using var exclusionTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                other.AcquireInterruptedReconciliationAsync(fixture.Session, exclusionTimeout.Token).AsTask());

            var pinned = lease.RequirePinnedSnapshot(journal);
            Assert.Equal(run.CreatedAtUtc, pinned.Evidence.ExecutionCreatedAtUtc);
            Assert.Equal(run.Revision + 1, pinned.Evidence.RunRevision);
            Assert.Equal(run.ToolAdmission!.Revision + 1, pinned.Evidence.JournalRevision);
            Assert.Equal(lease.Id, pinned.Evidence.DispatchLeaseId);
            Assert.Equal(run.ToolAdmission.Segments[0].OriginDispatchLeaseId, pinned.Admission.Run.ToolAdmission!.Segments[0].OriginDispatchLeaseId);
            Assert.NotEqual(lease.Id, pinned.Admission.Run.ToolAdmission.Segments[0].OriginDispatchLeaseId);
            observer.Pending.Add(new("foreign", "foreign", "foreign", "function", "foreign", "{}"));
            observer.Messages.Add(new(Guid.NewGuid(), ChatMessageRole.User, "Later mutable view", DateTimeOffset.UtcNow, 0));
            observer.Approvals.Add(new("foreign", run.Id, "foreign", "foreign", "function", "foreign", "{}",
                ExecutionApprovalStatus.Approved, DateTimeOffset.UtcNow, null, string.Empty, string.Empty, string.Empty));
            Assert.Empty(pinned.Admission.Run.PendingApprovals);
            Assert.Empty(pinned.Admission.ChatSession!.Messages);
            Assert.Empty(pinned.Approvals);
            Assert.Throws<AgentToolAdmissionException>(() => lease.Pin(journal, observer.Returned!));

            var observations = new List<AgentToolSessionObservation>();
            var readable = true;
            var claim = new AgentToolReceiptReconciliationClaim(journal, lease, Binding(run.ToolAdmission),
                currentRead: (observation, _) => {
                    observations.Add(observation);
                    return new(readable);
                });
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => claim.RequireAsync().AsTask());
            using (lease.Bind()) {
                for (var index = 0; index < 3; index++) {
                    var admitted = await claim.RequireAsync();
                    Assert.Same(pinned.Evidence, admitted.PinnedEvidence);
                    Assert.Equal(Binding(run.ToolAdmission), admitted.Binding);
                    Assert.Equal(run.ToolAdmission.Batches[0].Proposals[0].Payload, admitted.Payload);
                }
                Assert.Equal(3, observations.Count);
                Assert.All(observations, observation => {
                    Assert.Equal(run.ToolAdmission.Session, observation.Session);
                    Assert.Equal(AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson), observation.TurnContext);
                    Assert.Equal(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson), observation.Governance);
                });
                await Assert.ThrowsAsync<AgentToolAdmissionException>(() => other.RequireReceiptReconciliationAsync(claim, default).AsTask());
                var stale = new AgentToolReceiptReconciliationClaim(journal, lease,
                    Binding(run.ToolAdmission) with { Digest = AgentToolAdmissionJournalFixture.Payload(value: "changed").Digest },
                    currentRead: (_, _) => throw new InvalidOperationException("A mismatching intent cannot reach current policy."));
                await Assert.ThrowsAsync<AgentToolAdmissionException>(() => stale.RequireAsync().AsTask());
                readable = false;
                await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => claim.RequireAsync().AsTask());
                Assert.Equal(0, observer.RunReads);
                Assert.Equal(1, observer.Updates);
                await lease.DisposeAsync();
                await Assert.ThrowsAsync<AgentToolAdmissionException>(() => claim.RequireAsync().AsTask());
            }
            await using var next = await other.AcquireInterruptedReconciliationAsync(fixture.Session, default);
            Assert.NotEqual(lease.Id, next.Id);
            var retained = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!;
            Assert.Equal(run.ToolAdmission.Batches[0].Proposals[0], retained.Batches[0].Proposals[0]);
            Assert.Null(retained.Batches[0].Proposals[0].OwnerReceipt);
            Assert.Null(retained.Batches[0].Proposals[0].Result);
        } finally {
            await lease.DisposeAsync();
        }
    }

    private static ExecutionRunRecord EffectsRun(AgentToolAdmissionJournalFixture fixture) {
        var source = fixture.Detail.Run.ToolAdmission!;
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var originalLease = Guid.NewGuid();
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, "original-call", payload, false,
            AgentToolProposalState.Executing, ExecutionApprovalStatus.Approved, payload.Digest, DispatchClaimId: Guid.NewGuid());
        var journal = source with {
            SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion,
            Support = AgentToolAdmissionSupport.EffectJournalOnly,
            ActiveDispatchLeaseId = originalLease,
            Segments = [new(Guid.NewGuid(), 0, 0, null, PendingApprovals: [],
                CheckpointKind: AgentToolCheckpointKind.EffectsOnly, OriginDispatchLeaseId: originalLease)],
            Batches = [new(new(Guid.NewGuid()), 0, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(), [proposal])]
        };
        journal.Validate();
        return fixture.Detail.Run with { ToolAdmission = journal };
    }

    private static Task<AgentToolAdmissionJournalFixture> RestoreAsync(AgentToolAdmissionJournalFixture source, ExecutionRunRecord run)
        => AgentToolAdmissionJournalFixture.RestoreRetainedAsync(source.Agent, source.Provider, source.Profile, run);

    private static AgentToolApprovalBinding Binding(AgentToolJournalRecord journal) {
        var batch = journal.Batches[0];
        var proposal = batch.Proposals[0];
        return new(batch.Id, proposal.IntentId, proposal.Payload.SemanticVersion, proposal.Payload.Digest);
    }

    public interface IObservedStore : ISandboxWorkspaceStore, ISandboxWorkspaceExecutionRunStore,
        ISandboxWorkspaceExecutionRunMutationStore, ISandboxWorkspaceExecutionRunLeaseStore {
    }

    public class ObservedStoreProxy : DispatchProxy {
        internal FileSandboxWorkspaceStore Store { get; set; } = null!;
        internal int RunReads { get; private set; }
        internal int Updates { get; private set; }
        internal List<PendingToolApprovalRecord> Pending { get; } = [];
        internal List<ChatMessageRecord> Messages { get; } = [];
        internal List<ExecutionApprovalRecord> Approvals { get; } = [];
        internal ExecutionRunDetail? Returned { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name is nameof(ISandboxWorkspaceExecutionRunStore.GetExecutionRunAsync) or
                nameof(ISandboxWorkspaceExecutionRunStore.GetExecutionRunDetailAsync) or
                nameof(ISandboxWorkspaceExecutionRunStore.GetExecutionRunAdmissionSnapshotAsync)) {
                RunReads++;
            }
            object? result;
            try {
                result = targetMethod.Invoke(Store, args);
            } catch (TargetInvocationException failure) when (failure.InnerException is not null) {
                ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
            if (targetMethod.Name == nameof(ISandboxWorkspaceExecutionRunMutationStore.UpdateExecutionRunDetailAsync)) {
                Updates++;
                return ObserveAsync((Task<ExecutionRunDetail>)result!);
            }
            return result;
        }

        private async Task<ExecutionRunDetail> ObserveAsync(Task<ExecutionRunDetail> task) {
            var committed = await task;
            Pending.AddRange(committed.Run.PendingApprovals);
            Messages.AddRange(committed.ChatSession!.Messages);
            Approvals.AddRange(committed.Approvals);
            Returned = committed with {
                Run = committed.Run with { PendingApprovals = Pending },
                ChatSession = committed.ChatSession with { Messages = Messages },
                Approvals = Approvals
            };
            return Returned;
        }
    }
}
