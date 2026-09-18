using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentToolCancelledReadRecoveryIntegrationTests {
    [Theory]
    [InlineData(AgentToolProposalRecovery.RevalidateAndRead, false)]
    [InlineData(AgentToolProposalRecovery.RevalidateAndRead, true)]
    [InlineData(AgentToolProposalRecovery.ReconcileBeforeRetry, false)]
    [InlineData(AgentToolProposalRecovery.ReconcileBeforeRetry, true)]
    [InlineData(AgentToolProposalRecovery.OwnerReceipt, false)]
    [InlineData(AgentToolProposalRecovery.OwnerReceipt, true)]
    public async Task Cancelled_dispatched_read_preserves_the_original_recovery_contract_across_independent_stores(
        AgentToolProposalRecovery recovery, bool recordUncertainty) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var payload = ReadPayload(recovery);
        var journal = fixture.NewJournal();
        AgentToolBusinessIntentId intent;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var admitted = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("original-read", payload, false)], default);
            var claim = await journal.ClaimInvocationAsync(lease, admitted.Batches[0].Id, "original-read", payload, default);
            intent = claim.Proposal.IntentId;
            if (recordUncertainty) {
                await journal.PreserveUncertainInvocationAsync(claim, default);
            }
        }
        await CancelAsync(fixture);

        var expectedDisposition = recovery == AgentToolProposalRecovery.RevalidateAndRead
            ? AgentToolCancellationDisposition.NoExternalMutation : AgentToolCancellationDisposition.CancelledUnreconciled;
        var expectedEffect = recovery == AgentToolProposalRecovery.RevalidateAndRead
            ? AgentToolEffectState.None : AgentToolEffectState.Unknown;
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using (var lease = await restarted.AcquireCancelledReconciliationAsync(fixture.Session, default)) {
            var first = await restarted.ReconcileCancelledAsync(lease, [], currentReadAllowed: true, default);
            var outcome = Assert.Single(first.Outcomes);
            Assert.Equal(intent, outcome.IntentId);
            Assert.Equal(expectedEffect, outcome.EffectState);
            Assert.Equal(expectedDisposition, outcome.Cancellation!.Disposition);
            Assert.Equal(recovery == AgentToolProposalRecovery.RevalidateAndRead
                ? AgentToolCancellationReason.ReadOnlyInvocation : AgentToolCancellationReason.NoReceiptProtocol,
                outcome.Cancellation.Reason);
            Assert.Equal(recovery != AgentToolProposalRecovery.RevalidateAndRead, first.HasUnknownEffects);
        }

        var reopened = fixture.NewJournal(fixture.NewStore());
        await using (var lease = await reopened.AcquireCancelledReconciliationAsync(fixture.Session, default)) {
            var again = await reopened.ReconcileCancelledAsync(lease, [], currentReadAllowed: false, default);
            var outcome = Assert.Single(again.Outcomes);
            Assert.Equal(intent, outcome.IntentId);
            Assert.Equal(expectedEffect, outcome.EffectState);
            Assert.Equal(expectedDisposition, outcome.Cancellation!.Disposition);
        }
        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!;
        var retained = Assert.Single(Assert.Single(saved.ToolAdmission!.Batches).Proposals);
        Assert.Equal(payload, retained.Payload);
        Assert.NotNull(retained.DispatchClaimId);
        Assert.Equal(expectedEffect, retained.EffectState);
        Assert.Null(retained.Result);
        Assert.Equal(RunOutcome.Cancelled, saved.Outcome);
    }

    [Theory]
    [InlineData(AgentToolProposalRecovery.RevalidateAndRead)]
    [InlineData(AgentToolProposalRecovery.ReconcileBeforeRetry)]
    [InlineData(AgentToolProposalRecovery.OwnerReceipt)]
    public async Task Cancellation_before_dispatch_does_not_fabricate_metered_read_uncertainty(AgentToolProposalRecovery recovery) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var payload = ReadPayload(recovery);
        var journal = fixture.NewJournal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("never-dispatched", payload, false)], default);
        }
        await CancelAsync(fixture);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var reconciliation = await restarted.AcquireCancelledReconciliationAsync(fixture.Session, default);
        var result = await restarted.ReconcileCancelledAsync(reconciliation, [], currentReadAllowed: true, default);
        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(AgentToolCancellationDisposition.NotDispatched, outcome.Cancellation!.Disposition);
        Assert.Equal(AgentToolEffectState.None, outcome.EffectState);
        Assert.False(result.HasUnknownEffects);
        var retained = Assert.Single(Assert.Single((await fixture.NewStore().GetExecutionRunAsync(
            fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches).Proposals);
        Assert.Null(retained.DispatchClaimId);
    }

    private static AgentToolPreparedPayload ReadPayload(AgentToolProposalRecovery recovery) {
        var original = AgentToolAdmissionJournalFixture.Payload(recovery);
        return new(original.ToolName, original.SemanticVersion, original.Digest, original.ArgumentsJson,
            AgentToolProposalEffect.Read, recovery);
    }

    private static Task<ExecutionRunDetail> CancelAsync(AgentToolAdmissionJournalFixture fixture)
        => ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Store).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
            current => current with { Run = current.Run with {
                State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, CompletedAtUtc = DateTimeOffset.UtcNow,
                Revision = current.Run.Revision + 1
            } });
}
