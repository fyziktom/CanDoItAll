using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class AgentToolEffectJournalIntegrationTests {
    [Fact]
    public async Task Effect_origin_segment_is_idempotent_and_only_original_live_lease_can_append() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        await using var fixture = await RestoreAsync(source, EmptyEffectsRun(source));
        var owner = fixture.NewJournal();
        Guid segment;
        Guid origin;
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var envelope = AgentToolAdmissionJournalFixture.Envelope();
        await using (var lease = await owner.AcquireRunAsync(fixture.Session, default)) {
            using var held = lease.Bind();
            var first = await owner.BeginEffectSegmentAsync(lease, default);
            var repeated = await owner.BeginEffectSegmentAsync(lease, default);
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(repeated));
            var saved = Assert.Single(first.Segments);
            segment = saved.Id;
            origin = lease.Id;
            Assert.Equal(origin, saved.OriginDispatchLeaseId);
            Assert.Equal(AgentToolCheckpointKind.EffectsOnly, saved.CheckpointKind);
            Assert.Null(saved.RestartCheckpoint);
            Assert.Null(saved.ApprovalCheckpoint);
            var batch = await owner.AdmitBatchAsync(lease, payload.Digest, envelope, [new("first", payload, false)], default);
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.AdmitBatchAsync(lease,
                payload.Digest, envelope, [new("overtake", payload, false)], default));
            var invocation = await owner.ClaimInvocationAsync(lease, batch.Batches[0].Id, "first", payload, default);
            await owner.CompleteInvocationAsync(invocation, envelope, AgentToolEffectState.Committed, default);
            var next = await owner.AdmitBatchAsync(lease, payload.Digest, envelope, [new("second", payload, false)], default);
            Assert.Equal(segment, Assert.Single(next.Segments).Id);
            Assert.Equal(2, next.Batches.Length);
        }
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var replacement = await restarted.AcquireRunAsync(fixture.Session, default);
        using var bound = replacement.Bind();
        Assert.NotEqual(origin, replacement.Id);
        var before = JsonSerializer.Serialize(await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => restarted.BeginEffectSegmentAsync(replacement, default));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => restarted.AdmitBatchAsync(replacement,
            payload.Digest, envelope, [], default));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => restarted.BeginProviderDispatchAsync(replacement,
            segment, payload.Digest, payload.Digest, [], default));
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId)));
    }

    [Fact]
    public async Task Effect_approvals_bind_original_proposal_without_any_sdk_checkpoint() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        await using var fixture = await RestoreAsync(source, EmptyEffectsRun(source));
        var owner = fixture.NewJournal();
        await using var lease = await owner.AcquireRunAsync(fixture.Session, default);
        using var held = lease.Bind();
        var initial = await owner.BeginEffectSegmentAsync(lease, default);
        var segmentId = initial.Segments[0].Id;
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var envelope = AgentToolAdmissionJournalFixture.Envelope();
        var first = await owner.AdmitBatchAsync(lease, payload.Digest, envelope, [new("first", payload, true)], default);
        var pending = await owner.SaveEffectApprovalsAsync(lease, segmentId,
            [new("approval-1", "first", payload.ToolName, "function", "fixture", payload.ArgumentsJson)], default);
        Assert.Equal(Binding(first), Assert.Single(pending).ToolAdmission);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.SaveEffectApprovalsAsync(lease, segmentId, pending, default));
        await fixture.ApproveAsync(pending);
        var claim = await owner.ClaimInvocationAsync(lease, first.Batches[0].Id, "first", payload, default);
        await owner.CompleteInvocationAsync(claim, envelope, AgentToolEffectState.Committed, default);
        await owner.AdmitBatchAsync(lease, payload.Digest, envelope, [new("second", payload, true)], default);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.SaveEffectApprovalsAsync(lease, segmentId,
            [new("approval-1", "second", payload.ToolName, "function", "fixture", payload.ArgumentsJson)], default));
        var second = await owner.SaveEffectApprovalsAsync(lease, segmentId,
            [new("approval-2", "second", payload.ToolName, "function", "fixture", payload.ArgumentsJson)], default);
        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!;
        Assert.Null(saved.SerializedSessionStateJson);
        Assert.Equal(ExecutionState.WaitingOnTool, saved.State);
        Assert.Equal(second.ToArray(), saved.PendingApprovals.ToArray());
        var segment = Assert.Single(saved.ToolAdmission!.Segments);
        Assert.Equal(segmentId, segment.Id);
        Assert.Equal(2, segment.PendingApprovals.Length);
        Assert.Equal(pending[0], segment.PendingApprovals[0]);
        Assert.Equal(second[0], segment.PendingApprovals[1]);
        Assert.Null(segment.RestartCheckpoint);
        Assert.Null(segment.ApprovalCheckpoint);
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(saved.ToolAdmission,
            saved.ToolAdmission with {
                Revision = saved.ToolAdmission.Revision + 1,
                Segments = [segment with { PendingApprovals = segment.PendingApprovals.SetItem(0, pending[0] with { Details = "changed" }) }]
            }));
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Store).UpdateExecutionRunDetailAsync(saved.Id, detail => detail with {
            Run = detail.Run with { State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, Revision = detail.Run.Revision + 1 }
        });
        var cancelled = JsonSerializer.Serialize(await fixture.NewStore().GetExecutionRunAsync(saved.Id));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.SaveEffectApprovalsAsync(lease, segmentId, second, default));
        Assert.Equal(cancelled, JsonSerializer.Serialize(await fixture.NewStore().GetExecutionRunAsync(saved.Id)));
    }

    [Fact]
    public async Task Effect_provider_dispatch_preserves_schema_five_and_exact_native_response_fences() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        await using var fixture = await RestoreAsync(source, EmptyEffectsRun(source));
        var owner = fixture.NewJournal();
        await using var lease = await owner.AcquireRunAsync(fixture.Session, default);
        using var held = lease.Bind();
        var saved = await owner.BeginEffectSegmentAsync(lease, default);
        var segmentId = saved.Segments[0].Id;
        var payload = AgentToolAdmissionJournalFixture.Payload(AgentToolProposalRecovery.ReconcileBeforeRetry);
        var envelope = AgentToolAdmissionJournalFixture.Envelope();
        var request = await owner.BeginProviderDispatchAsync(lease, segmentId, payload.Digest, payload.Digest, [], default);
        saved = await owner.CompleteProviderDispatchAsync(lease, request.Id, payload.Digest, envelope,
            [new("native", payload, true, new("native-owner", payload.Digest, envelope))], default);
        Assert.Equal(AgentToolJournalRecord.EffectJournalSchemaVersion, saved.SchemaVersion);
        var binding = Binding(saved);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.BeginProviderDispatchAsync(lease,
            segmentId, payload.Digest, payload.Digest, [binding], default));
        var pending = await owner.SaveEffectApprovalsAsync(lease, segmentId,
            [new("native-approval", "native", payload.ToolName, "mcp", "native-owner", payload.ArgumentsJson)], default);
        await fixture.ApproveAsync(pending);
        var dispatch = await owner.BeginProviderDispatchAsync(lease, segmentId, payload.Digest, payload.Digest, [binding], default);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.BeginProviderDispatchAsync(lease,
            segmentId, payload.Digest, payload.Digest, [], default));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.CompleteProviderDispatchAsync(lease,
            dispatch.Id, AgentToolProtocolEnvelope.ComputeDigest("different"), envelope, [], default));
        saved = await owner.CompleteProviderDispatchAsync(lease, dispatch.Id, payload.Digest, envelope, [], default);
        Assert.Equal(AgentToolJournalRecord.EffectJournalSchemaVersion, saved.SchemaVersion);
        Assert.Equal(segmentId, Assert.Single(saved.Segments).Id);
        Assert.Equal(lease.Id, saved.Segments[0].OriginDispatchLeaseId);
        var completed = AgentToolJournalTransitions.RequireProposal(saved, binding);
        Assert.Equal(AgentToolProposalState.Completed, completed.State);
        Assert.Equal(dispatch.Id.Value, completed.DispatchClaimId);
        Assert.Equal(envelope, completed.Result);
        Assert.Equal(AgentToolEffectState.Unknown, completed.EffectState);
        saved.Validate();
    }

    private static ExecutionRunRecord EmptyEffectsRun(AgentToolAdmissionJournalFixture fixture) {
        var run = EffectsRun(fixture);
        return run with { ToolAdmission = run.ToolAdmission! with { Segments = [], Batches = [], ActiveDispatchLeaseId = null } };
    }
}
