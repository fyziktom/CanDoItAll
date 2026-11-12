using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class AgentToolEffectJournalIntegrationTests {
    [Fact]
    public async Task Cancelled_reconciliation_preserves_cancelled_disposition_and_never_dispatches() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        foreach (var alreadyConfirmed in new[] { false, true }) {
            var run = EffectsRun(source);
            await using var fixture = await RestoreAsync(source, run);
            if (alreadyConfirmed) {
                await ReconcileAsync(fixture, new ReceiptReader());
            }
            var cancelled = await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore()).UpdateExecutionRunDetailAsync(run.Id,
                current => current with { Run = current.Run with {
                    Revision = current.Run.Revision + 1, State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled,
                    CompletedAtUtc = DateTimeOffset.UtcNow
                } });
            var owner = fixture.NewJournal(fixture.NewStore());
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.AcquireInterruptedReconciliationAsync(fixture.Session, default).AsTask());
            await using var lease = await owner.AcquireCancelledReconciliationAsync(fixture.Session, default);
            using var bound = lease.Bind();
            var reader = new ReceiptReader();
            var result = await owner.ReconcileCancelledAsync(lease, [reader], currentReadAllowed: true, default);
            Assert.Equal(alreadyConfirmed ? 0 : 1, reader.Calls);
            var saved = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!;
            Assert.Equal(RunOutcome.Cancelled, saved.Outcome);
            Assert.Equal(ExecutionState.Failed, saved.State);
            Assert.Equal(cancelled.Run.SerializedSessionStateJson, saved.SerializedSessionStateJson);
            var proposal = saved.ToolAdmission!.Batches[0].Proposals[0];
            Assert.Equal(run.ToolAdmission!.Batches[0].Proposals[0].Payload, proposal.Payload);
            Assert.Equal(run.ToolAdmission.Batches[0].Proposals[0].DispatchClaimId, proposal.DispatchClaimId);
            Assert.Equal(AgentToolEffectState.Committed, proposal.EffectState);
            Assert.Null(proposal.Result);
            if (alreadyConfirmed) {
                Assert.Equal(cancelled.Run.ToolAdmission!.Batches[0].Proposals[0], proposal);
                Assert.Null(proposal.Cancellation);
            } else {
                Assert.Equal(AgentToolProposalState.Cancelled, proposal.State);
                Assert.Equal(AgentToolCancellationDisposition.ReceiptCommitted, proposal.Cancellation!.Disposition);
            }
            Assert.Equal(AgentToolEffectState.Committed, Assert.Single(result.Outcomes).EffectState);
        }
    }

    [Fact]
    public async Task Prepared_work_is_a_receipt_noop_while_native_uncertainty_requires_reconciliation() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var prepared = ChangeProposal(EffectsRun(source), proposal => proposal with {
            State = AgentToolProposalState.Prepared, DispatchClaimId = null
        });
        var pending = WithPendingApproval(EffectsRun(source, 2));
        pending = ChangeProposal(pending, proposal => proposal with { State = AgentToolProposalState.Prepared, DispatchClaimId = null });
        var missingInput = EmptyEffectsRun(source);
        var native = EffectsRun(source);
        native = native with { ToolAdmission = native.ToolAdmission! with {
            Batches = [], ProviderDispatches = [new(new(Guid.NewGuid()), 0, native.ToolAdmission.Segments[0].Id,
                AgentToolAdmissionJournalFixture.Payload().Digest, AgentToolAdmissionJournalFixture.Payload().Digest, [], AgentToolProviderDispatchState.Started)]
        } };
        (ExecutionRunRecord Run, AgentToolContinuationRequirement Requirement, int Prepared, int Pending)[] cases = [
            (prepared, AgentToolContinuationRequirement.None, 1, 0),
            (pending, AgentToolContinuationRequirement.None, 2, 1),
            (missingInput, AgentToolContinuationRequirement.ProviderInputRequired, 0, 0),
            (native, AgentToolContinuationRequirement.EffectReconciliationRequired, 0, 0)
        ];
        foreach (var item in cases) {
            await using var fixture = await RestoreAsync(source, item.Run);
            var forbidden = new ReceiptReader((_, _) => throw new InvalidOperationException("Non-receipt work cannot invoke an owner or provider."));
            var result = await ReconcileAsync(fixture, forbidden);
            Assert.Equal(0, forbidden.Calls);
            Assert.Equal(item.Requirement, result.Requirement);
            Assert.Equal(item.Prepared, result.Counts.PreparedEffects);
            Assert.Equal(item.Pending, result.Counts.PendingApprovals);
            Assert.Equal(item.Prepared != 0, result.HasPreparedEffects);
            Assert.Empty(result.Observations);
            var saved = (await fixture.NewStore().GetExecutionRunAsync(item.Run.Id))!;
            Assert.Equal(JsonSerializer.Serialize(item.Run.ToolAdmission!.Batches), JsonSerializer.Serialize(saved.ToolAdmission!.Batches));
            Assert.Equal(item.Run.SerializedSessionStateJson, saved.SerializedSessionStateJson);
            Assert.Equal(item.Run.PendingApprovals, saved.PendingApprovals);
        }
    }
}
