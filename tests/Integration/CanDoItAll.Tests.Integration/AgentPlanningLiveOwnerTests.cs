using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using static CanDoItAll.Tests.Support.AgentPlanningDecisionTestData;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentPlanningLiveOwnerTests {
    [Fact]
    public async Task Initial_factory_requires_a_genuine_new_run_and_matching_original_agent() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var draft = Create(source.Detail.Run.ToolAdmission!.Session);
        var journal = source.NewJournal();
        Assert.Throws<AgentToolAdmissionException>(() => journal.CreateForNewPlanningRun(source.Detail.Run, source.Detail.ChatSession!,
            source.Detail.Run.ToolAdmission.OriginalInput!, draft.PlanningDecision!.OriginalTarget, draft.PlanningDecision.Provenance!));
        Assert.Throws<InvalidDataException>(() => journal.CreateForNewPlanningRun(source.Detail.Run with { ToolAdmission = null },
            source.Detail.ChatSession!, source.Detail.Run.ToolAdmission.OriginalInput!, draft.PlanningDecision!.OriginalTarget,
            draft.PlanningDecision.Provenance! with { AgentId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task Start_requires_bound_real_lease_and_exact_current_expectation() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var original = AgentPlanningDecisionExpectation.From(await fixture.ReadAsync());
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.StartAsync(lease));
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Owner.StartStepAsync(lease, original, default));
        Assert.Null((await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work.Open);
        var work = await fixture.StartAsync(lease);
        var retained = (await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!;
        Assert.Equal(lease.Id, retained.Work.Open!.DispatchLeaseId);
        Assert.Equal(work.IntervalId, retained.Work.Open.Id);
        var anotherOwner = new AgentPlanningDecisionOwner(fixture.Journal);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => anotherOwner.CaptureBasisAsync(work,
            (_, _) => throw new InvalidOperationException("The foreign handle must not capture data."), default));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, 1)]
    [InlineData(ReadBasisMode.StepSnapshot, 2)]
    public async Task Successive_steps_use_the_selected_material_basis_and_charge_without_a_gap(ReadBasisMode mode, int capturesExpected) {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(mode);
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var captures = 0;
        Task<AgentToolProtocolEnvelope> Capture(AgentPlanningDecisionRecord _, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            captures++;
            return Task.FromResult(Envelope(captures == 1 ? "{\"tasks\":[1]}" : "{\"tasks\":[1,2]}"));
        }
        var first = await fixture.StartAsync(lease);
        await fixture.Owner.CaptureBasisAsync(first, Capture, default);
        var dispatch = await fixture.EnterAsync(first);
        Assert.Null((await fixture.RespondAsync(first, dispatch)).Refusal);
        fixture.Clock.Now += TimeSpan.FromSeconds(5);
        fixture.Clock.Timestamp += TimeSpan.FromSeconds(10).Ticks;
        var second = await fixture.Owner.AdvanceStepAsync(first, default);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.SettleTerminalAsync(first,
            AgentPlanningGenerationState.Completed, default));
        var between = (await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!;
        Assert.Equal(TimeSpan.FromSeconds(10).Ticks, between.Work.ClosedWorkTicks);
        Assert.Equal(between.Work.LastSettlement!.EndedAtUtc, between.Work.Open!.OpenedAtUtc);
        await fixture.Owner.CaptureBasisAsync(second, Capture, default);
        var next = await fixture.EnterAsync(second);
        await fixture.RespondAsync(second, next);
        fixture.Clock.Advance(TimeSpan.FromSeconds(3));
        var completed = await fixture.Owner.SettleTerminalAsync(second, AgentPlanningGenerationState.Completed, default);
        var decision = completed.ToolAdmission!.PlanningDecision!;
        Assert.Equal(capturesExpected, captures);
        Assert.Equal(capturesExpected, decision.Current.Bases.Length);
        Assert.Equal(mode == ReadBasisMode.RunSnapshot, decision.Current.Steps[0].BasisId == decision.Current.Steps[1].BasisId);
        Assert.Equal(TimeSpan.FromSeconds(13).Ticks, decision.Work.ClosedWorkTicks);
        Assert.Null(decision.Work.Open);
        Assert.Equal(RunOutcome.Succeeded, completed.Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Either_clock_at_the_work_limit_refuses_transport_entry(bool monotonic) {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        if (monotonic) {
            fixture.Clock.Timestamp += TimeSpan.FromMinutes(5).Ticks;
        } else {
            fixture.Clock.Now += TimeSpan.FromMinutes(5);
        }
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.EnterAsync(work));
        Assert.Equal(AgentPlanningAdmissionRefusal.WorkBudgetExhausted, failure.Reason);
        Assert.True((await fixture.ReadAsync()).ToolAdmission!.ProviderDispatches.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task Approval_wait_closes_the_interval_and_saves_all_original_bindings_together() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        await fixture.RespondAsync(work, dispatch, publish: true);
        fixture.Clock.Advance(TimeSpan.FromSeconds(4));
        var call = AgentPlanningLiveOwnerFixture.Publication();
        var approval = new PendingToolApprovalRecord("approval-1", call.CallId, call.Payload.ToolName, "function", "Review tasks.", "{}");
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.PauseForApprovalAsync(work,
            approval with { ArgumentsJson = "{\"changed\":true}" }, default));
        Assert.NotNull((await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work.Open);
        var waiting = await fixture.Owner.PauseForApprovalAsync(work, approval, default);
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        var retained = await fixture.ReadAsync();
        Assert.Equal(ExecutionState.WaitingOnTool, retained.State);
        Assert.Equal(waiting.PendingApprovals.Single(), retained.ToolAdmission!.Segments.Single().PendingApprovals.Single());
        Assert.Equal(retained.ToolAdmission.PlanningDecision!.Current.Steps.Single().Publications.Single().Proposal,
            retained.PendingApprovals.Single().ToolAdmission);
        Assert.Equal("approval-1", retained.ToolAdmission.Batches.Single().Proposals.Single().ApprovalId);
        Assert.Equal(TimeSpan.FromSeconds(4).Ticks, retained.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
        Assert.Null(retained.ToolAdmission.PlanningDecision.Work.Open);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.StartAsync(lease));
    }

    [Fact]
    public async Task Cancellation_while_provider_is_running_retains_the_real_response_without_new_publication() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        await CancelThroughExecutionMergeAsync(fixture);
        fixture.Clock.Advance(TimeSpan.FromSeconds(8));
        var response = await fixture.RespondAsync(work, dispatch, publish: true);
        Assert.Equal(AgentPlanningAdmissionRefusal.DecisionInactive, response.Refusal);
        Assert.Equal("{\"actualResponse\":true}", response.Run.ToolAdmission!.Batches.Single().Response.PayloadJson);
        Assert.Empty(response.Run.ToolAdmission.Batches.Single().Proposals);
        Assert.Equal(AgentPlanningGenerationState.Cancelled, response.Run.ToolAdmission.PlanningDecision!.Current.State);
        var settled = await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Completed, default);
        Assert.Equal(RunOutcome.Cancelled, settled.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(8).Ticks, settled.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
    }

    [Fact]
    public async Task Clock_rollback_preserves_known_response_and_unsettled_accounting() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        fixture.Clock.Now -= TimeSpan.FromSeconds(1);
        var response = await fixture.RespondAsync(work, dispatch, publish: true);
        Assert.Equal(AgentPlanningAdmissionRefusal.ClockInvalid, response.Refusal);
        Assert.Equal(work.OriginalRun.UpdatedAtUtc, response.Run.UpdatedAtUtc);
        Assert.Empty(response.Run.ToolAdmission!.Batches.Single().Proposals);
        Assert.Equal(AgentPlanningGenerationState.Failed, response.Run.ToolAdmission.PlanningDecision!.Current.State);
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.SettleTerminalAsync(work,
            AgentPlanningGenerationState.Failed, default));
        Assert.Equal(AgentPlanningAdmissionRefusal.ClockInvalid, failure.Reason);
        var retained = (await fixture.ReadAsync()).ToolAdmission!;
        Assert.Single(retained.Batches);
        Assert.Equal(work.IntervalId, retained.PlanningDecision!.Work.Open!.Id);
        Assert.Equal(0, retained.PlanningDecision.Work.ClosedWorkTicks);
    }

    [Fact]
    public async Task Monotonic_rollback_after_a_positive_observation_cannot_reduce_spent_work() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        fixture.Clock.Timestamp = TimeSpan.FromSeconds(10).Ticks;
        await fixture.CaptureAsync(work);
        fixture.Clock.Timestamp = TimeSpan.FromSeconds(5).Ticks;
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.EnterAsync(work));
        Assert.Equal(AgentPlanningAdmissionRefusal.ClockInvalid, failure.Reason);
    }

    [Fact]
    public async Task Budget_exhausted_by_a_provider_preserves_output_and_charges_actual_elapsed_work() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        var response = await fixture.RespondAsync(work, dispatch, publish: true);
        Assert.Equal(AgentPlanningAdmissionRefusal.WorkBudgetExhausted, response.Refusal);
        Assert.Empty(response.Run.ToolAdmission!.Batches.Single().Proposals);
        var settled = await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Failed, default);
        Assert.Equal(TimeSpan.FromMinutes(6).Ticks, settled.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Equal(RunOutcome.Failed, settled.Outcome);
    }

    [Fact]
    public async Task Invalid_publication_contract_does_not_discard_actual_provider_output() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        var response = await fixture.Owner.AdmitResponseAsync(work, dispatch.Id, dispatch.RequestDigest, Envelope("{\"actual\":1}"),
            AgentPlanningLiveOwnerFixture.Publication() with { RequiresApproval = false }, default);
        Assert.Equal(AgentPlanningAdmissionRefusal.InvalidPublication, response.Refusal);
        Assert.Empty(response.Run.ToolAdmission!.Batches.Single().Proposals);
        Assert.Equal("{\"actual\":1}", response.Run.ToolAdmission.Batches.Single().Response.PayloadJson);
    }

    [Fact]
    public async Task One_provider_origin_cannot_be_reentered_or_accept_a_different_request() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.EnterAsync(work));
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.AdmitResponseAsync(work, dispatch.Id,
            Envelope("{\"different\":1}").Digest, Envelope(), null, default));
        var retained = (await fixture.ReadAsync()).ToolAdmission!;
        Assert.Single(retained.ProviderDispatches);
        Assert.Empty(retained.Batches);
        await fixture.RespondAsync(work, dispatch);
    }

    [Fact]
    public async Task Lost_start_acknowledgement_cannot_create_a_fresh_interval_or_clock() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using (var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default)) {
            using var bound = lease.Bind();
            fixture.Fault = ExistingRunDetailCommitStage.RunPersisted;
            AgentToolAdmissionJournalFixture.RequireScriptedFault(
                await Assert.ThrowsAnyAsync<Exception>(() => fixture.StartAsync(lease)), "planning-publication-ack");
            Assert.NotNull((await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work.Open);
        }
        var restartedJournal = new AgentToolAdmissionJournal(fixture.Files.NewStore(), fixture.Files.Profile, fixture.Clock);
        var restarted = new AgentPlanningDecisionOwner(restartedJournal);
        await using var nextLease = await restartedJournal.AcquireRunAsync(fixture.Files.Session, default);
        using var rebound = nextLease.Bind();
        var saved = await fixture.ReadAsync();
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => restarted.StartStepAsync(nextLease,
            AgentPlanningDecisionExpectation.From(saved), default));
        Assert.Equal(AgentPlanningAdmissionRefusal.InterruptedWorkAccountingUnavailable, failure.Reason);
        Assert.Equal(saved.ToolAdmission!.PlanningDecision!.Work.Open, (await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work.Open);
    }

    [Fact]
    public async Task Lost_response_acknowledgement_never_creates_a_second_batch() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        fixture.Fault = ExistingRunDetailCommitStage.RunPersisted;
        AgentToolAdmissionJournalFixture.RequireScriptedFault(
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.RespondAsync(work, dispatch, publish: true)), "planning-publication-ack");
        var saved = (await fixture.ReadAsync()).ToolAdmission!;
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.RespondAsync(work, dispatch, publish: true));
        var after = (await fixture.ReadAsync()).ToolAdmission!;
        Assert.Equal(saved.Batches.Single().Id, after.Batches.Single().Id);
        Assert.Equal(saved.Batches.Single().Proposals.Single().IntentId, after.Batches.Single().Proposals.Single().IntentId);
    }

    [Fact]
    public async Task Lost_settlement_acknowledgement_cannot_charge_the_interval_twice() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        await fixture.RespondAsync(work, dispatch);
        fixture.Clock.Advance(TimeSpan.FromSeconds(7));
        fixture.Fault = ExistingRunDetailCommitStage.RunPersisted;
        AgentToolAdmissionJournalFixture.RequireScriptedFault(await Assert.ThrowsAnyAsync<Exception>(() =>
            fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Completed, default)), "planning-publication-ack");
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.SettleTerminalAsync(work,
            AgentPlanningGenerationState.Completed, default));
        var retained = await fixture.ReadAsync();
        Assert.Equal(RunOutcome.Succeeded, retained.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(7).Ticks, retained.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Null(retained.ToolAdmission.PlanningDecision.Work.Open);
    }

    [Fact]
    public async Task Start_acknowledgement_time_is_charged_and_disposed_lease_cannot_continue() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        fixture.OnCommit = stage => {
            if (stage == ExistingRunDetailCommitStage.RunPersisted) {
                fixture.OnCommit = null;
                fixture.Clock.Advance(TimeSpan.FromSeconds(3));
            }
        };
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        await fixture.RespondAsync(work, dispatch);
        var completed = await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Completed, default);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, completed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        await lease.DisposeAsync();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.CaptureAsync(work));
    }

    [Fact]
    public async Task Failed_basis_capture_keeps_the_original_work_interval_and_no_provider_origin() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await Assert.ThrowsAsync<IOException>(() => fixture.Owner.CaptureBasisAsync(work,
            (_, _) => throw new IOException("basis-owner-unavailable"), default));
        var saved = (await fixture.ReadAsync()).ToolAdmission!;
        Assert.Equal(work.IntervalId, saved.PlanningDecision!.Work.Open!.Id);
        Assert.Empty(saved.PlanningDecision.Current.Bases);
        Assert.True(saved.ProviderDispatches.IsDefaultOrEmpty);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.StartAsync(lease));
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        var failed = await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Failed, default);
        Assert.Equal(TimeSpan.FromSeconds(2).Ticks, failed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execution_cancellation_retires_unentered_publications_without_resetting_accounting(bool failedFirst) {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        await fixture.RespondAsync(work, dispatch, publish: true);
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        if (failedFirst) {
            await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Failed, default);
        }
        var before = (await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work;
        await CancelThroughExecutionMergeAsync(fixture);
        var cancelled = (await fixture.ReadAsync()).ToolAdmission!;
        var proposal = cancelled.Batches.Single().Proposals.Single();
        Assert.Equal(AgentToolProposalState.Cancelled, proposal.State);
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        Assert.Equal(AgentPlanningGenerationState.Cancelled, cancelled.PlanningDecision!.Current.State);
        Assert.Equal(before.Open, cancelled.PlanningDecision.Work.Open);
        Assert.Equal(before.ClosedWorkTicks, cancelled.PlanningDecision.Work.ClosedWorkTicks);
        Assert.False(cancelled.HasUnresolvedEffects);
    }

    private static Task<ExecutionRunDetail> CancelThroughExecutionMergeAsync(AgentPlanningLiveOwnerFixture fixture)
        => ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Files.NewStore()).UpdateExecutionRunDetailAsync(fixture.Files.Session.ExecutionRunId,
            current => current with { Run = current.Run with {
                Revision = current.Run.Revision + 1, Outcome = RunOutcome.Cancelled, State = ExecutionState.Completed,
                ToolAdmission = AgentToolJournalTransitions.RejectUndispatchedOnCancellation(current.Run.ToolAdmission!)
            } });
}
