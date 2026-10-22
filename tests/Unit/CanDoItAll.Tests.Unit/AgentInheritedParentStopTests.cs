using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using static CanDoItAll.Tests.Support.AgentPlanningDecisionTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentInheritedParentStopTests {
    [Fact]
    public async Task Idle_selected_stop_retains_work_and_replays_without_another_revision() {
        var fixture = new AgentInheritedParentStopFixture();
        var original = fixture.Detail!.Run.ToolAdmission!.PlanningDecision!;
        Assert.Equal(AgentInheritedPlanningStopOutcome.Stopped, await fixture.StopAsync());
        var cancelled = fixture.Detail!.Run;
        Assert.Equal(RunOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(AgentPlanningGenerationState.Cancelled, cancelled.ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Equal(original.Work.ClosedWorkTicks, cancelled.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
        Assert.Equal(original.InheritedOrigin, cancelled.ToolAdmission.PlanningDecision.InheritedOrigin);
        Assert.Equal(3, fixture.ParentChecks);
        Assert.True(fixture.LeaseDisposed);
        Assert.Equal(AgentInheritedPlanningStopOutcome.AlreadyClosed, await fixture.StopAsync());
        Assert.Same(cancelled, fixture.Detail.Run);
        Assert.Equal(1, fixture.Writes);
    }

    [Fact]
    public async Task A_live_registration_receives_a_signal_without_journal_mutation_or_lease_wait() {
        var fixture = new AgentInheritedParentStopFixture();
        var original = fixture.Detail!;
        using var registration = fixture.Registry.Register(original.Run, default);
        Assert.Equal(AgentInheritedPlanningStopOutcome.Signalled, await fixture.StopAsync());
        Assert.True(registration.Token.IsCancellationRequested);
        Assert.Same(original, fixture.Detail);
        Assert.Equal(0, fixture.Writes);
        Assert.Equal(0, fixture.Leases);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Parent_revocation_before_signal_after_lease_or_before_publication_makes_no_write(int boundary) {
        var fixture = new AgentInheritedParentStopFixture();
        var original = fixture.Detail;
        fixture.OnParentCheck = count => {
            if (count == boundary) {
                throw new UnauthorizedAccessException("The original stopped-parent capability changed.");
            }
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.StopAsync());
        Assert.Same(original, fixture.Detail);
        Assert.Equal(0, fixture.Writes);
        Assert.Equal(boundary > 1, fixture.LeaseDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Open_work_is_not_closed_even_when_no_local_execution_is_registered(bool duringWrite) {
        var fixture = new AgentInheritedParentStopFixture();
        void Open() => fixture.SetJournal(Begin(fixture.Detail!.Run.ToolAdmission!));
        if (duringWrite) {
            fixture.OnWrite = Open;
        } else {
            Open();
        }
        Assert.Equal(AgentInheritedPlanningStopOutcome.OpenWork, await fixture.StopAsync());
        Assert.NotNull(fixture.Detail!.Run.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(0, fixture.Writes);
    }

    [Fact]
    public async Task Closed_work_with_an_unresolved_provider_entry_remains_pending() {
        var fixture = new AgentInheritedParentStopFixture();
        var initial = fixture.Detail!.Run.ToolAdmission!;
        var provider = Enter(Attach(Begin(initial), CanDoItAll.Tests.Support.AgentInheritedPlanningTestData.Basis(initial)));
        var decision = provider.PlanningDecision!;
        fixture.SetJournal(Changed(provider, provider with {
            PlanningDecision = AgentPlanningDecisionTransitions.Settle(decision, decision.Work.Open!.Id, TimeSpan.FromSeconds(2),
                Start.AddSeconds(2), AgentPlanningWorkSettlementReason.ReconciliationOnly, AgentPlanningGenerationState.Failed)
        }));
        var before = JsonSerializer.Serialize(fixture.Detail);
        Assert.Equal(AgentInheritedPlanningStopOutcome.UnresolvedEffects, await fixture.StopAsync());
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Detail));
        Assert.Equal(0, fixture.Writes);
    }

    [Theory]
    [InlineData(AgentToolProposalState.Executing, AgentToolEffectState.Unknown, AgentInheritedPlanningStopOutcome.UnresolvedEffects)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, AgentToolEffectState.Unknown, AgentInheritedPlanningStopOutcome.UnresolvedEffects)]
    [InlineData(AgentToolProposalState.Completed, AgentToolEffectState.Unknown, AgentInheritedPlanningStopOutcome.UnresolvedEffects)]
    [InlineData(AgentToolProposalState.Completed, AgentToolEffectState.Committed, AgentInheritedPlanningStopOutcome.CompletionPending)]
    public async Task Dispatched_effects_require_their_original_reconciliation_or_completion_owner(
        AgentToolProposalState state, AgentToolEffectState effect, AgentInheritedPlanningStopOutcome expected) {
        var fixture = new AgentInheritedParentStopFixture();
        var initial = fixture.Detail!.Run.ToolAdmission!;
        var waiting = AwaitApproval(Respond(Enter(Attach(Begin(initial),
            CanDoItAll.Tests.Support.AgentInheritedPlanningTestData.Basis(initial)))), TimeSpan.FromSeconds(2));
        var batch = Assert.Single(waiting.Batches);
        var proposal = Assert.Single(batch.Proposals);
        fixture.SetJournal(Changed(waiting, waiting with {
            Batches = [batch with { Proposals = [proposal with {
                State = state, EffectState = effect, ApprovalStatus = ExecutionApprovalStatus.Approved,
                ApprovedDigest = proposal.Payload.Digest, DispatchClaimId = Guid.NewGuid(),
                Result = state == AgentToolProposalState.Completed ? Envelope("{\"effect\":\"observed\"}") : null
            }] }]
        }));
        var before = JsonSerializer.Serialize(fixture.Detail);
        Assert.Equal(expected, await fixture.StopAsync());
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Detail));
        Assert.Equal(0, fixture.Writes);
    }

    [Fact]
    public async Task A_changed_immutable_run_after_lease_wait_refuses_before_a_write() {
        var fixture = new AgentInheritedParentStopFixture();
        fixture.OnLease = () => fixture.Detail = fixture.Detail! with {
            Run = fixture.Detail.Run with { CorrelationId = "changed-correlation" }
        };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.StopAsync());
        Assert.Equal(0, fixture.Writes);
        Assert.True(fixture.LeaseDisposed);
    }

    [Fact]
    public async Task A_backward_clock_is_rejected_without_changing_the_journal() {
        var fixture = new AgentInheritedParentStopFixture();
        fixture.Clock.Now = Start.AddTicks(-1);
        var original = fixture.Detail;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.StopAsync());
        Assert.Same(original, fixture.Detail);
        Assert.Equal(0, fixture.Writes);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_a_stop_acknowledgement() {
        var fixture = new AgentInheritedParentStopFixture();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.StopAsync(cancelled.Token));
        Assert.Equal(0, fixture.ParentChecks);
        Assert.Equal(0, fixture.Writes);
    }

    [Fact]
    public async Task Missing_selected_journal_remains_explicitly_missing() {
        var fixture = new AgentInheritedParentStopFixture();
        var id = fixture.Detail!.Run.Id;
        fixture.Detail = null;
        Assert.Equal(AgentInheritedPlanningStopOutcome.MissingJournal, await fixture.Owner.StopAsync(id, fixture.Parent));
        Assert.Equal(0, fixture.Leases);
        Assert.Equal(0, fixture.Writes);
    }

    [Fact]
    public async Task Malformed_retained_journal_is_rejected_before_signalling_or_mutation() {
        var fixture = new AgentInheritedParentStopFixture();
        fixture.Detail = fixture.Detail! with { Run = fixture.Detail.Run with {
            ToolAdmission = fixture.Detail.Run.ToolAdmission! with { Revision = 0 }
        } };
        using var registration = fixture.Registry.Register(fixture.Detail.Run, default);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.StopAsync());
        Assert.False(registration.Token.IsCancellationRequested);
        Assert.Equal(0, fixture.ParentChecks);
        Assert.Equal(0, fixture.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Another_original_parent_or_profile_cannot_stop_the_execution(bool profile) {
        var fixture = new AgentInheritedParentStopFixture();
        var original = fixture.Original;
        var changed = profile ? original with { Profile = new(Guid.NewGuid(), original.Profile.Fingerprint, original.Profile.Generation) }
            : original with { BasisId = Guid.NewGuid() };
        var capability = new AgentInheritedParentStop(changed, (_, _) => Task.CompletedTask);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Owner.StopAsync(fixture.Detail!.Run.Id, capability));
        Assert.Equal(0, fixture.Leases);
        Assert.Equal(0, fixture.Writes);
    }

    [Fact]
    public async Task Inconsistent_terminal_run_is_not_declared_closed_or_rewritten() {
        var fixture = new AgentInheritedParentStopFixture();
        Assert.Equal(AgentInheritedPlanningStopOutcome.Stopped, await fixture.StopAsync());
        fixture.Detail = fixture.Detail! with { Run = fixture.Detail.Run with { State = ExecutionState.Running } };
        var changed = fixture.Detail;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.StopAsync());
        Assert.Same(changed, fixture.Detail);
        Assert.Equal(1, fixture.Writes);
    }
}
