using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentToolAdmissionJournalIntegrationTests {
    [Fact]
    public async Task Request_scoped_input_keeps_session_authorization_but_cannot_admit_durable_effects_or_change_its_support() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(AgentToolAdmissionSupport.RequestScopedInput);
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var current = lease.Bind();
        Assert.Equal(fixture.Session, (await journal.RequireSessionAsync(fixture.Session)).Reference);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.BeginSegmentAsync(lease,
            AgentToolAdmissionJournalFixture.Envelope(), null, default));
        Assert.Equal("tool-admission.request-scoped-input", denied.Code);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Store).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
                detail => detail with { Run = detail.Run with { ToolAdmission = detail.Run.ToolAdmission! with {
                    Support = AgentToolAdmissionSupport.Recoverable, Revision = detail.Run.ToolAdmission.Revision + 1
                } } }));
        Assert.Empty((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Segments);
    }

    [Fact]
    public async Task Independent_stores_serialize_dispatch_without_blocking_the_short_checkpoint_lock() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var first = fixture.NewJournal();
        await using (var lease = await first.AcquireRunAsync(fixture.Session, default)) {
            using var current = lease.Bind();
            await first.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await fixture.NewJournal(fixture.NewStore()).AcquireRunAsync(fixture.Session, cancellation.Token));
            var saved = await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId);
            Assert.Single(saved!.ToolAdmission!.Segments);
        }

        await using var next = await fixture.NewJournal(fixture.NewStore()).AcquireRunAsync(fixture.Session, default);
        Assert.NotEqual(Guid.Empty, next.Id);
    }

    [Fact]
    public async Task One_assistant_batch_allocates_distinct_durable_intents_for_identical_legitimate_creates() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var current = lease.Bind();
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("provider-call-a", payload, true), new("provider-call-b", payload, true)], default);
        var restarted = await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId);
        var proposals = Assert.Single(restarted!.ToolAdmission!.Batches).Proposals;
        Assert.Equal(2, proposals.Length);
        Assert.NotEqual(proposals[0].IntentId, proposals[1].IntentId);
        Assert.Equal(payload, proposals[0].Payload);
        Assert.Equal(payload, proposals[1].Payload);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await journal.ClaimInvocationAsync(lease,
            restarted.ToolAdmission.Batches[0].Id, "provider-call-a", payload, default));
    }

    [Theory]
    [InlineData((int)ExistingRunDetailCommitStage.JournalPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.RunPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.WorkspaceIndexPersisted)]
    public async Task Lost_file_commit_ack_recovers_the_original_batch_and_intent(int stageValue) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var fail = false;
        var failingStore = fixture.NewStore(stage => {
            if (fail && stage == (ExistingRunDetailCommitStage)stageValue) {
                fail = false;
                throw new IOException("Injected admission checkpoint acknowledgement loss.");
            }
        });
        var journal = fixture.NewJournal(failingStore);
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var current = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            fail = true;
            var payload = AgentToolAdmissionJournalFixture.Payload();
            await Assert.ThrowsAsync<IOException>(() => journal.AdmitBatchAsync(lease, payload.Digest,
                AgentToolAdmissionJournalFixture.Envelope(), [new("correlation-only", payload, true)], default));
        }

        var recoveredStore = fixture.NewStore();
        var saved = await recoveredStore.GetExecutionRunAsync(fixture.Session.ExecutionRunId);
        var batch = Assert.Single(saved!.ToolAdmission!.Batches);
        var intent = Assert.Single(batch.Proposals).IntentId;
        var secondRead = await fixture.NewStore().GetExecutionRunAsync(saved.Id);
        Assert.Equal(intent, Assert.Single(Assert.Single(secondRead!.ToolAdmission!.Batches).Proposals).IntentId);
        Assert.Equal(batch.Id, Assert.Single(secondRead.ToolAdmission.Batches).Id);
    }

    [Fact]
    public async Task A_stale_detail_or_whole_workspace_cannot_clear_a_current_batch() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var staleState = await fixture.Store.LoadExecutionAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var current = lease.Bind();
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(), [new("call", payload, true)], default);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => fixture.NewStore().SaveExecutionRunDetailAsync(fixture.Detail));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => fixture.NewStore().SaveExecutionAsync(staleState));
        Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches);
    }

    [Theory]
    [InlineData(AgentToolProposalRecovery.OwnerReceipt, false)]
    [InlineData(AgentToolProposalRecovery.RevalidateAndRead, false)]
    [InlineData(AgentToolProposalRecovery.ReconcileBeforeRetry, true)]
    public async Task Reentry_preserves_server_intent_and_requires_reconciliation_for_uncertain_non_idempotent_effects(
        AgentToolProposalRecovery recovery, bool mustReconcile) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var payload = AgentToolAdmissionJournalFixture.Payload(recovery);
        AgentToolBatchRecord batch;
        var initial = fixture.NewJournal();
        await using (var lease = await initial.AcquireRunAsync(fixture.Session, default)) {
            using var current = lease.Bind();
            await initial.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var saved = await initial.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("call", payload, false)], default);
            batch = Assert.Single(saved.Batches);
            await initial.ClaimInvocationAsync(lease, batch.Id, "call", payload, default);
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var restartLease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var bound = restartLease.Bind();
        if (mustReconcile) {
            var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(async () =>
                await restarted.ClaimInvocationAsync(restartLease, batch.Id, "call", payload, default));
            Assert.Equal("tool-admission.reconciliation-required", failure.Code);
        } else {
            var claim = await restarted.ClaimInvocationAsync(restartLease, batch.Id, "call", payload, default);
            using var dispatch = claim.Bind();
            var admitted = await restarted.RequireInvocationAsync(fixture.Session, payload.ToolName, payload.Digest);
            Assert.Equal(Assert.Single(batch.Proposals).IntentId, admitted.IntentId);
            await restarted.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope("{\"originalId\":42}"),
                AgentToolEffectState.Committed, default);
        }
    }

    [Fact]
    public async Task Approval_checkpoint_and_exact_digest_survive_restart_and_reject_changed_payload() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var current = lease.Bind();
        var first = await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var admitted = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("call", payload, true)], default);
        var pending = await journal.SaveApprovalCheckpointAsync(lease, first.Segments[0].Id,
            AgentToolAdmissionJournalFixture.Envelope("{\"sdkApprovalId\":\"approval-7\"}"), "{\"sdk\":\"pending\"}",
            [new("approval-7", "call", payload.ToolName, "function", "Review", payload.ArgumentsJson)], default);
        var persisted = await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId);
        Assert.Equal(pending[0].ToolAdmission, Assert.Single(persisted!.PendingApprovals).ToolAdmission);
        await fixture.ApproveAsync(pending);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await journal.ClaimInvocationAsync(lease,
            admitted.Batches[0].Id, "call", AgentToolAdmissionJournalFixture.Payload(value: "changed"), default));
        var claim = await journal.ClaimInvocationAsync(lease, admitted.Batches[0].Id, "call", payload, default);
        Assert.Equal(pending[0].ToolAdmission!.IntentId, claim.Proposal.IntentId);
    }

    [Fact]
    public async Task A_released_or_foreign_runtime_lease_cannot_verify_an_invocation() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var current = lease.Bind();
        await lease.DisposeAsync();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await journal.RequireSessionAsync(fixture.Session));
    }
}
