using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentInheritedEffectReconciliationTests {
    [Fact]
    public async Task Receipt_recovery_preserves_original_work_and_binding_without_dispatch_authority() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var uncertain = await PrepareUncertainAsync(fixture);
        var session = uncertain.ToolAdmission!.Session.Reference;
        var binding = Binding(uncertain);
        var original = AgentToolJournalTransitions.RequireProposal(uncertain.ToolAdmission, binding);
        fixture.Policy.DispatchAllowed = false;
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        await using var lease = await fixture.Journal.AcquireInterruptedReconciliationAsync(session, default);
        using var bound = lease.Bind();
        Assert.Equal(AgentToolRunLeaseMode.InterruptedEffectReconciliation, lease.Mode);
        Assert.Null(lease.PlanningWork);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Journal.RequireSessionAsync(session).AsTask());
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            fixture.Journal.ClaimSavedInvocationAsync(lease, binding, original.Payload, default));
        var reader = new ReceiptReader(Confirmed);
        var result = await fixture.Journal.ReconcileInterruptedAsync(lease, [reader], AgentInheritedPlanningFixture.AllowRead, default);
        Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, result.Persistence);
        Assert.Equal(1, result.Counts.KnownCommitted);
        Assert.Equal(1, reader.Calls);
        var recovered = await fixture.ReadAsync();
        Assert.Equal(JsonSerializer.Serialize(uncertain.ToolAdmission.PlanningDecision),
            JsonSerializer.Serialize(recovered.ToolAdmission!.PlanningDecision));
        Assert.Equal(original with {
            State = AgentToolProposalState.OwnerReceiptConfirmed, EffectState = AgentToolEffectState.Committed,
            OwnerReceipt = new(Confirmed.CommittedEffect!, Confirmed.Receipt!)
        }, AgentToolJournalTransitions.RequireProposal(recovered.ToolAdmission, binding));
        Assert.Equal(ExecutionState.WaitingOnTool, recovered.State);
        Assert.Null(recovered.Outcome);
    }

    [Theory]
    [InlineData(ReadDenial.Caller, false)]
    [InlineData(ReadDenial.Caller, true)]
    [InlineData(ReadDenial.Source, false)]
    [InlineData(ReadDenial.Source, true)]
    [InlineData(ReadDenial.SourceIdentity, false)]
    [InlineData(ReadDenial.SourceIdentity, true)]
    public async Task Read_revocation_before_or_during_receipt_lookup_cannot_persist_or_disclose_confirmation(
        ReadDenial denial, bool duringLookup) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var uncertain = await PrepareUncertainAsync(fixture);
        var callerAllowed = true;
        void Revoke() {
            switch (denial) {
                case ReadDenial.Caller:
                    callerAllowed = false;
                    break;
                case ReadDenial.Source:
                    fixture.Policy.ReadAllowed = false;
                    break;
                case ReadDenial.SourceIdentity:
                    fixture.Policy.Fingerprint = AgentPlanningDecisionTestData.Envelope("{\"changedOwner\":1}").Digest;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(denial));
            }
        }
        if (!duringLookup) {
            Revoke();
        }
        await using var lease = await fixture.Journal.AcquireInterruptedReconciliationAsync(uncertain.ToolAdmission!.Session.Reference, default);
        using var bound = lease.Bind();
        var reader = new ReceiptReader(Confirmed, duringLookup ? Revoke : null);
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() =>
            fixture.Journal.ReconcileInterruptedAsync(lease, [reader], (_, _) => new(callerAllowed), default));
        Assert.Equal(duringLookup ? 1 : 0, reader.Calls);
        var retained = await fixture.ReadAsync();
        Assert.Equal(JsonSerializer.Serialize(uncertain.ToolAdmission.PlanningDecision),
            JsonSerializer.Serialize(retained.ToolAdmission!.PlanningDecision));
        Assert.Equal(AgentToolJournalTransitions.RequireProposal(uncertain.ToolAdmission, Binding(uncertain)),
            AgentToolJournalTransitions.RequireProposal(retained.ToolAdmission, Binding(uncertain)));
        Assert.Null(retained.ToolAdmission.ReceiptReconciliationCursor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_different_profile_or_original_source_cannot_acquire_the_inherited_recovery_lease(bool profileChanged) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var uncertain = await PrepareUncertainAsync(fixture);
        var session = uncertain.ToolAdmission!.Session.Reference;
        var journal = profileChanged
            ? new AgentToolAdmissionJournal(fixture.Files.NewStore(), new(Guid.NewGuid(), "different-profile", new(1)),
                fixture.Clock, backgroundSources: [fixture.Policy])
            : fixture.Journal;
        if (!profileChanged) {
            var source = session.BackgroundSource!;
            session = new(session.ExecutionRunId, Guid.Empty, default,
                new(source.SourceKind, "different-source", source.OwnerFingerprint, source.ExecutionFingerprint));
        }
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.AcquireInterruptedReconciliationAsync(session, default).AsTask());
        Assert.Equal(JsonSerializer.Serialize(uncertain), JsonSerializer.Serialize(await fixture.ReadAsync()));
        await using var correct = await fixture.Journal.AcquireInterruptedReconciliationAsync(uncertain.ToolAdmission.Session.Reference, default);
        Assert.Null(correct.PlanningWork);
    }

    [Fact]
    public async Task Independent_recovery_cannot_steal_the_original_held_run_lease() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var uncertain = await PrepareUncertainAsync(fixture);
        var session = uncertain.ToolAdmission!.Session.Reference;
        var independent = fixture.NewJournal();
        await using (var first = await fixture.Journal.AcquireInterruptedReconciliationAsync(session, default)) {
            var retained = await fixture.ReadAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                independent.AcquireInterruptedReconciliationAsync(session, deadline.Token).AsTask());
            Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(await fixture.ReadAsync()));
        }
        await using var next = await independent.AcquireInterruptedReconciliationAsync(session, default);
        Assert.Null(next.PlanningWork);
    }

    private static AgentToolReceiptObservation Confirmed => new(new("inherited-receipt-fixture", "original-effect"),
        AgentPlanningDecisionTestData.Envelope("{\"receipt\":1}"));

    private static AgentToolApprovalBinding Binding(ExecutionRunRecord run)
        => run.ToolAdmission!.PlanningDecision!.Current.Steps[^1].Publications.Single().Proposal;

    private static async Task<ExecutionRunRecord> PrepareUncertainAsync(AgentInheritedPlanningFixture fixture) {
        var waiting = await fixture.WaitForApprovalAsync();
        var binding = Binding(waiting);
        var request = new AgentSavedEffectContinuationRequest(waiting.Id, null, waiting.Revision, waiting.ToolAdmission!.Revision,
            binding, waiting.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) {
                ToolAdmission = item.ToolAdmission
            }).ToArray());
        await using var lease = await fixture.Owner.ResumeInheritedSavedEffectAsync(request,
            AgentPlanningDecisionExpectation.From(waiting),
            new(waiting.ToolAdmission.PlanningDecision!.InheritedOrigin!, (_, _) => Task.CompletedTask),
            AgentInheritedPlanningFixture.AllowRead, default);
        using var bound = lease.Bind();
        await fixture.Journal.ObserveSavedEffectReceiptAsync(lease, new ReceiptReader(AgentToolReceiptObservation.NotObserved), default);
        var proposal = AgentToolJournalTransitions.RequireProposal(waiting.ToolAdmission, binding);
        var claim = await fixture.Journal.ClaimSavedInvocationAsync(lease, binding, proposal.Payload, default);
        using var claimed = claim.Bind();
        await fixture.Journal.PreserveUncertainInvocationAsync(claim, default);
        var uncertain = await fixture.ReadAsync();
        Assert.Equal(AgentToolProposalState.ReconciliationRequired,
            AgentToolJournalTransitions.RequireProposal(uncertain.ToolAdmission!, binding).State);
        return uncertain;
    }

    private sealed class ReceiptReader(AgentToolReceiptObservation observation, Action? afterRead = null)
        : IAgentToolReceiptReconciliationProvider {
        internal int Calls { get; private set; }

        public bool Supports(string toolName) => toolName == AgentPlanningLiveOwnerFixture.Publication().Payload.ToolName;

        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            await claim.RequireAsync(cancellationToken);
            Calls++;
            afterRead?.Invoke();
            return observation;
        }
    }

    public enum ReadDenial { Caller, Source, SourceIdentity }
}
