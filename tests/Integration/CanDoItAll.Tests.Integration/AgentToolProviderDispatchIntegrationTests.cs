using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentToolProviderDispatchIntegrationTests {
    [Fact]
    public async Task Unacknowledged_remote_request_survives_independent_store_restart_and_cannot_redispatch() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        AgentToolProviderDispatchRecord first;
        var journal = fixture.NewJournal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            var saved = await journal.BeginSegmentAsync(lease, Envelope(), null, default);
            first = await journal.BeginProviderDispatchAsync(lease, saved.Segments[0].Id, Digest, Digest, [], default);
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var current = await restarted.AcquireRunAsync(fixture.Session, default);
        var recovered = await restarted.ReadAsync(current, default);
        Assert.Equal(AgentToolJournalRecord.ProviderDispatchSchemaVersion, recovered.SchemaVersion);
        Assert.True(recovered.HasUnresolvedEffects);
        Assert.Equal(first, Assert.Single(recovered.ProviderDispatches));
        Assert.Empty(recovered.Batches);
        var error = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => restarted.BeginProviderDispatchAsync(
            current, recovered.Segments[0].Id, Digest, Digest, [], default));
        Assert.Equal("tool-admission.reconciliation-required", error.Code);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => restarted.AdmitBatchAsync(current, Digest, Envelope(), [], default));
    }

    [Theory]
    [InlineData((int)ExistingRunDetailCommitStage.JournalPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.RunPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.WorkspaceIndexPersisted)]
    public async Task Lost_response_checkpoint_ack_recovers_one_atomic_response_and_dispatch(int stage) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var inject = false;
        var store = fixture.NewStore(current => {
            if (inject && current == (ExistingRunDetailCommitStage)stage) {
                inject = false;
                throw new IOException("Injected response checkpoint acknowledgement loss.");
            }
        });
        var journal = fixture.NewJournal(store);
        AgentToolProviderDispatchRecord dispatch;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            var saved = await journal.BeginSegmentAsync(lease, Envelope(), null, default);
            dispatch = await journal.BeginProviderDispatchAsync(lease, saved.Segments[0].Id, Digest, Digest, [], default);
            inject = true;
            await Assert.ThrowsAsync<IOException>(() => journal.CompleteProviderDispatchAsync(lease, dispatch.Id,
                Digest, Envelope("{\"observed\":true}"), [], default));
        }

        var savedRun = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!;
        var completed = Assert.Single(savedRun.ToolAdmission!.ProviderDispatches);
        var batch = Assert.Single(savedRun.ToolAdmission.Batches);
        Assert.Equal(AgentToolProviderDispatchState.ResponseAdmitted, completed.State);
        Assert.Equal(dispatch.Id, completed.Id);
        Assert.Equal(batch.Id, completed.ResponseBatchId);
        Assert.Equal(dispatch.Id, batch.ProviderDispatchId);
        Assert.False(savedRun.ToolAdmission.HasUnresolvedEffects);
    }

    [Fact]
    public async Task Native_approval_requires_exact_saved_decision_and_checkpoints_with_the_remote_response() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        var saved = await journal.BeginSegmentAsync(lease, Envelope(), null, default);
        var request = await journal.BeginProviderDispatchAsync(lease, saved.Segments[0].Id, Digest, Digest, [], default);
        var payload = AgentToolAdmissionJournalFixture.Payload(AgentToolProposalRecovery.ReconcileBeforeRetry);
        saved = await journal.CompleteProviderDispatchAsync(lease, request.Id, Digest, Envelope(),
            [new("native-call", payload, true, new("native-server", Digest, Envelope()))], default);
        var proposal = Assert.Single(saved.Batches[0].Proposals);
        var binding = new AgentToolApprovalBinding(saved.Batches[0].Id, proposal.IntentId, payload.SemanticVersion, payload.Digest);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.ClaimInvocationAsync(lease, saved.Batches[0].Id,
            "native-call", payload, default));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.BeginProviderDispatchAsync(lease,
            saved.Segments[0].Id, Digest, Digest, [binding], default));

        var pending = await journal.SaveApprovalCheckpointAsync(lease, saved.Segments[0].Id, Envelope(), "{}",
            [new("native-approval", "native-call", payload.ToolName, "mcp", "native-server", payload.ArgumentsJson)], default);
        await fixture.ApproveAsync(pending);
        saved = await journal.BeginSegmentAsync(lease, Envelope(), saved.Segments[0].Id, default);
        var effectRequest = await journal.BeginProviderDispatchAsync(lease, saved.Segments[^1].Id, Digest, Digest, [binding], default);
        var executing = AgentToolJournalTransitions.RequireProposal(await journal.ReadAsync(lease, default), binding);
        Assert.Equal(AgentToolProposalState.Executing, executing.State);
        Assert.Equal(effectRequest.Id.Value, executing.DispatchClaimId);
        saved = await journal.CompleteProviderDispatchAsync(lease, effectRequest.Id, Digest, Envelope("{\"nativeResult\":42}"), [], default);
        var completed = AgentToolJournalTransitions.RequireProposal(saved, binding);
        Assert.Equal(AgentToolProposalState.Completed, completed.State);
        Assert.Equal(AgentToolEffectState.Unknown, completed.EffectState);
        Assert.Equal(Assert.Single(saved.Batches[0].Proposals).IntentId, proposal.IntentId);
        Assert.Equal(saved.Batches[1].Response, completed.Result);
    }

    [Fact]
    public async Task Stale_replacement_or_schema_downgrade_cannot_erase_a_remote_dispatch() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        var before = await journal.BeginSegmentAsync(lease, Envelope(), null, default);
        await journal.BeginProviderDispatchAsync(lease, before.Segments[0].Id, Digest, Digest, [], default);
        var store = (ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => store.UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
            detail => detail with { Run = detail.Run with { ToolAdmission = before with { Revision = detail.Run.ToolAdmission!.Revision + 1 } } }));
        var retained = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Single(retained.ProviderDispatches);
        Assert.Throws<InvalidDataException>(() => (retained with { SchemaVersion = AgentToolJournalRecord.CurrentSchemaVersion }).Validate());
        var restored = JsonSerializer.Deserialize<AgentToolJournalRecord>(JsonSerializer.Serialize(retained))!;
        restored.Validate();
        Assert.Equal(retained.ProviderDispatches[0].Id, restored.ProviderDispatches[0].Id);
    }

    [Fact]
    public async Task Cancellation_keeps_unknown_remote_dispatch_visible_without_fabricating_a_tool_intent() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        AgentToolProviderDispatchRecord dispatched;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            var saved = await journal.BeginSegmentAsync(lease, Envelope(), null, default);
            dispatched = await journal.BeginProviderDispatchAsync(lease, saved.Segments[0].Id, Digest, Digest, [], default);
        }
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Store).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
            detail => detail with { Run = detail.Run with { State = ExecutionState.Completed, Outcome = RunOutcome.Cancelled,
                Revision = detail.Run.Revision + 1 } });
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var current = await restarted.AcquireCancelledReconciliationAsync(fixture.Session, default);
        var result = await restarted.ReconcileCancelledAsync(current, [], currentReadAllowed: true, default);
        Assert.True(result.HasUnknownEffects);
        Assert.Empty(result.Outcomes);
        var outcome = Assert.Single(result.ProviderDispatches!);
        Assert.Equal(dispatched.Id, outcome.Id);
        Assert.Equal(AgentToolProviderDispatchState.CancelledUnreconciled, outcome.State);
        var second = await restarted.ReconcileCancelledAsync(current, [], currentReadAllowed: true, default);
        Assert.Equal(outcome, Assert.Single(second.ProviderDispatches!));
    }

    private static AgentToolSemanticDigest Digest => AgentToolProtocolEnvelope.ComputeDigest("native-contract");
    private static AgentToolProtocolEnvelope Envelope(string json = "{}") => AgentToolAdmissionJournalFixture.Envelope(json);
}
