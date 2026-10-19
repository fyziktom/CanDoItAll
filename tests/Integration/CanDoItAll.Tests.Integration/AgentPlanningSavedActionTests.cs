using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentPlanningSavedActionTests {
    [Fact]
    public async Task Resumed_work_excludes_human_wait_and_requires_the_actual_claim_and_held_selected_snapshot() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(organizationScope: true);
        var waiting = await WaitForApprovalAsync(fixture, TimeSpan.FromMinutes(2));
        var original = waiting.ToolAdmission!;
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        var work = Assert.IsType<AgentPlanningLiveWork>(lease.PlanningWork);
        var binding = Binding(waiting);
        var resumed = await fixture.ReadAsync();
        Assert.Equal(TimeSpan.FromMinutes(2).Ticks, resumed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Equal(original.PlanningDecision!.Policy, resumed.ToolAdmission.PlanningDecision.Policy);
        Assert.Equal(original.PlanningDecision.Current.Steps[0].OriginDispatchLeaseId,
            resumed.ToolAdmission.PlanningDecision.Current.Steps[0].OriginDispatchLeaseId);
        Assert.NotEqual(original.PlanningDecision.Current.Steps[0].OriginDispatchLeaseId, lease.Id);
        await using (var held = await SnapshotAsync(fixture)) {
            Assert.Throws<AgentPlanningAdmissionException>(() => work.RequirePublication(held, binding));
        }
        var claim = await ClaimAsync(fixture, lease, waiting);
        using var claimed = claim.Bind();
        var publication = await SnapshotAsync(fixture);
        await using (publication) {
            Assert.Equal(TimeSpan.FromMinutes(3), work.RequirePublication(publication, binding));
            Assert.Throws<AgentPlanningAdmissionException>(() => work.RequirePublication(publication,
                binding with { IntentId = new(Guid.NewGuid()) }));
            fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(TimeSpan.FromMinutes(3) - TimeSpan.FromSeconds(5), work.RequirePublication(publication, binding));
        }
        Assert.Throws<ObjectDisposedException>(() => work.RequirePublication(publication, binding));
        await fixture.Journal.CompleteInvocationAsync(claim, AgentPlanningDecisionTestData.Envelope("{\"noEffect\":true}"),
            AgentToolEffectState.NotCommitted, default);
        var result = await fixture.Journal.CheckpointSavedEffectAsync(lease, default);
        Assert.Equal(AgentToolEffectState.NotCommitted, result.EffectState);
        var settled = await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Completed, default);
        Assert.Equal(TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(5)).Ticks, settled.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Null(settled.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Equal(RunOutcome.Succeeded, settled.Outcome);
    }

    [Fact]
    public async Task Work_expiration_before_the_claim_cannot_enter_an_effect() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var waiting = await WaitForApprovalAsync(fixture, TimeSpan.FromMinutes(4));
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var refused = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => ClaimAsync(fixture, lease, waiting));
        Assert.Equal(AgentPlanningAdmissionRefusal.WorkBudgetExhausted, refused.Reason);
        Assert.Null(AgentToolJournalTransitions.RequireProposal((await fixture.ReadAsync()).ToolAdmission!, Binding(waiting)).DispatchClaimId);
        var failed = await fixture.Owner.SettleTerminalAsync(lease.PlanningWork!, AgentPlanningGenerationState.Failed, default);
        Assert.Equal(TimeSpan.FromMinutes(5).Ticks, failed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
    }

    [Fact]
    public async Task Work_expiration_after_the_claim_is_rechecked_inside_the_held_publication_authority() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(organizationScope: true);
        var waiting = await WaitForApprovalAsync(fixture, TimeSpan.FromMinutes(4));
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        var claim = await ClaimAsync(fixture, lease, waiting);
        using var claimed = claim.Bind();
        await using (var held = await SnapshotAsync(fixture)) {
            Assert.Equal(TimeSpan.FromMinutes(1), lease.PlanningWork!.RequirePublication(held, Binding(waiting)));
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));
            var refused = Assert.Throws<AgentPlanningAdmissionException>(() => lease.PlanningWork.RequirePublication(held, Binding(waiting)));
            Assert.Equal(AgentPlanningAdmissionRefusal.WorkBudgetExhausted, refused.Reason);
        }
        await fixture.Journal.CompleteInvocationAsync(claim, AgentPlanningDecisionTestData.Envelope("{\"expiredBeforeEffect\":true}"),
            AgentToolEffectState.NotCommitted, default);
        await fixture.Journal.CheckpointSavedEffectAsync(lease, default);
        var failed = await fixture.Owner.SettleTerminalAsync(lease.PlanningWork!, AgentPlanningGenerationState.Failed, default);
        Assert.Equal(TimeSpan.FromMinutes(5).Ticks, failed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
    }

    [Theory]
    [InlineData(InvalidAction.LegacyAdmission)]
    [InlineData(InvalidAction.Decision)]
    [InlineData(InvalidAction.Generation)]
    [InlineData(InvalidAction.Binding)]
    public async Task Wrong_original_action_cannot_change_the_approval_or_open_work(InvalidAction kind) {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var waiting = await WaitForApprovalAsync(fixture);
        var expected = AgentPlanningDecisionExpectation.From(waiting);
        var request = Request(waiting);
        if (kind is InvalidAction.Decision or InvalidAction.Generation) {
            expected = new(expected.ExecutionRunId, expected.RunRevision, expected.JournalRevision,
                kind == InvalidAction.Decision ? Guid.NewGuid() : expected.DecisionId,
                expected.Generation + (kind == InvalidAction.Generation ? 1 : 0));
        }
        if (kind == InvalidAction.Binding) {
            request = new(waiting.Id, waiting.ChatSessionId, waiting.Revision, waiting.ToolAdmission!.Revision,
                request.Binding with { IntentId = new(Guid.NewGuid()) }, request.Decisions);
        }
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => kind == InvalidAction.LegacyAdmission
            ? fixture.Journal.AcquireSavedEffectDispatchAsync(request, AllowRead, default)
            : fixture.Owner.ResumeSavedEffectAsync(request, expected, AllowRead, default));
        var unchanged = await fixture.ReadAsync();
        Assert.Equal(AgentPlanningDecisionExpectation.From(waiting), AgentPlanningDecisionExpectation.From(unchanged));
        Assert.Null(unchanged.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(ExecutionApprovalStatus.Pending,
            AgentToolJournalTransitions.RequireProposal(unchanged.ToolAdmission, Binding(waiting)).ApprovalStatus);
    }

    [Fact]
    public async Task Current_read_revocation_refuses_resume_refresh_and_cancel_without_retiring_the_proposal() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var waiting = await WaitForApprovalAsync(fixture);
        var expected = AgentPlanningDecisionExpectation.From(waiting);
        static ValueTask<bool> Deny(AgentToolSessionObservation observation, CancellationToken token) => new(false);
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Owner.ResumeSavedEffectAsync(Request(waiting), expected, Deny, default));
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Owner.RefreshAsync(expected, false, Deny, default));
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Owner.CancelAsync(expected, Deny, default));
        Assert.Equal(expected, AgentPlanningDecisionExpectation.From(await fixture.ReadAsync()));
    }

    [Fact]
    public async Task Two_independent_owners_cannot_resume_the_same_displayed_approval() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var waiting = await WaitForApprovalAsync(fixture);
        var second = new AgentPlanningDecisionOwner(new(fixture.Files.NewStore(), fixture.Files.Profile, fixture.Clock));
        async Task<(AgentToolRunLease? Lease, Exception? Failure)> Attempt(AgentPlanningDecisionOwner owner) {
            try {
                return (await owner.ResumeSavedEffectAsync(Request(waiting), AgentPlanningDecisionExpectation.From(waiting), AllowRead, default), null);
            } catch (Exception failure) {
                return (null, failure);
            }
        }
        var attempts = new[] { Attempt(fixture.Owner), Attempt(second) };
        var first = await (await Task.WhenAny(attempts));
        if (first.Lease is not null) {
            await first.Lease.DisposeAsync();
        }
        var results = await Task.WhenAll(attempts);
        foreach (var result in results) {
            if (result.Lease is not null) {
                await result.Lease.DisposeAsync();
            }
        }
        Assert.Single(results, item => item.Lease is not null);
        Assert.IsType<AgentToolAdmissionException>(Assert.Single(results, item => item.Failure is not null).Failure);
        var saved = await fixture.ReadAsync();
        Assert.Equal(waiting.Revision + 1, saved.Revision);
        Assert.NotNull(saved.ToolAdmission!.PlanningDecision!.Work.Open);
    }

    [Fact]
    public async Task Lost_resume_acknowledgement_leaves_an_open_interval_that_cannot_be_reconstructed() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var waiting = await WaitForApprovalAsync(fixture);
        fixture.Fault = ExistingRunDetailCommitStage.RunPersisted;
        await Assert.ThrowsAsync<IOException>(() => ResumeAsync(fixture, waiting));
        var retained = await fixture.ReadAsync();
        Assert.NotNull(retained.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Empty(retained.PendingApprovals);
        var error = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => ResumeAsync(fixture, retained));
        Assert.Equal(AgentPlanningAdmissionRefusal.WorkAlreadyOpen, error.Reason);
        Assert.Null(AgentToolJournalTransitions.RequireProposal(retained.ToolAdmission, Binding(retained)).DispatchClaimId);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.RefreshAsync(
            AgentPlanningDecisionExpectation.From(retained), false, AllowRead, default));
    }

    [Fact]
    public async Task Refresh_retires_original_approval_and_recaptures_run_snapshot_under_a_new_generation() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(ReadBasisMode.RunSnapshot);
        var waiting = await WaitForApprovalAsync(fixture, TimeSpan.FromSeconds(7));
        var original = waiting.ToolAdmission!.PlanningDecision!;
        var refreshed = await fixture.Owner.RefreshAsync(AgentPlanningDecisionExpectation.From(waiting), false, AllowRead, default);
        var decision = refreshed.Run.ToolAdmission!.PlanningDecision!;
        Assert.Equal(2, decision.CurrentGeneration);
        Assert.Equal(original.Policy, decision.Policy);
        Assert.Equal(original.Work.ClosedWorkTicks, decision.Work.ClosedWorkTicks);
        Assert.Equal(AgentPlanningGenerationState.Superseded, decision.Generations[0].State);
        Assert.Equal(AgentToolProposalState.Rejected,
            AgentToolJournalTransitions.RequireProposal(refreshed.Run.ToolAdmission, Binding(waiting)).State);
        Assert.Equal(AgentToolEffectState.NotCommitted,
            AgentToolJournalTransitions.RequireProposal(refreshed.Run.ToolAdmission, Binding(waiting)).EffectState);
        Assert.Empty(refreshed.Run.PendingApprovals);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => ResumeAsync(fixture, waiting));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Owner.CancelAsync(
            AgentPlanningDecisionExpectation.From(waiting), AllowRead, default));
        var replacement = await WaitForApprovalAsync(fixture);
        Assert.NotEqual(Binding(waiting), Binding(replacement));
        Assert.NotEqual(original.Current.Bases[0].Id, replacement.ToolAdmission!.PlanningDecision!.Current.Bases[0].Id);
        Assert.Single(replacement.PendingApprovals);
        Assert.Equal(TimeSpan.FromSeconds(8).Ticks, replacement.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
    }

    [Fact]
    public async Task Expired_basis_cannot_resume_but_explicit_cancellation_needs_no_new_work_budget() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var waiting = await WaitForApprovalAsync(fixture, TimeSpan.FromSeconds(3));
        fixture.Clock.Advance(TimeSpan.FromDays(2));
        var error = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => ResumeAsync(fixture, waiting));
        Assert.Equal(AgentPlanningAdmissionRefusal.BasisExpired, error.Reason);
        var cancelled = await fixture.Owner.CancelAsync(AgentPlanningDecisionExpectation.From(waiting), AllowRead, default);
        Assert.Equal(RunOutcome.Cancelled, cancelled.Run.Outcome);
        Assert.Equal(AgentPlanningGenerationState.Cancelled, cancelled.Run.ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, cancelled.Run.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
        Assert.Null(cancelled.Run.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Empty(cancelled.Run.PendingApprovals);
    }

    [Fact]
    public async Task Automatic_replan_requires_a_definite_no_effect_checkpoint_and_keeps_the_original_limit() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(replans: 1);
        var waiting = await WaitForApprovalAsync(fixture);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.RefreshAsync(
            AgentPlanningDecisionExpectation.From(waiting), true, AllowRead, default));
        var paused = await RecordNoEffectPauseAsync(fixture, waiting);
        var refreshed = await fixture.Owner.RefreshAsync(AgentPlanningDecisionExpectation.From(paused), true, AllowRead, default);
        Assert.Equal(1, refreshed.Run.ToolAdmission!.PlanningDecision!.AutomaticReplans);
        var next = await WaitForApprovalAsync(fixture);
        Assert.NotEqual(Binding(waiting), Binding(next));
        var secondPause = await RecordNoEffectPauseAsync(fixture, next);
        var error = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.RefreshAsync(
            AgentPlanningDecisionExpectation.From(secondPause), true, AllowRead, default));
        Assert.Equal(AgentPlanningAdmissionRefusal.ReplanLimit, error.Reason);
        var explicitRefresh = await fixture.Owner.RefreshAsync(AgentPlanningDecisionExpectation.From(secondPause), false, AllowRead, default);
        Assert.Equal(1, explicitRefresh.Run.ToolAdmission!.PlanningDecision!.AutomaticReplans);
        Assert.Equal(3, explicitRefresh.Run.ToolAdmission.PlanningDecision.CurrentGeneration);
        Assert.Equal(secondPause.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks,
            explicitRefresh.Run.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
    }

    [Theory]
    [InlineData(AgentToolEffectState.Committed)]
    [InlineData(AgentToolEffectState.Unknown)]
    public async Task Committed_or_uncertain_effects_cannot_be_reclassified_as_a_basis_change(AgentToolEffectState effect) {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(replans: 1);
        var waiting = await WaitForApprovalAsync(fixture);
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        var claim = await ClaimAsync(fixture, lease, waiting);
        using var claimed = claim.Bind();
        await fixture.Journal.CompleteInvocationAsync(claim, AgentPlanningDecisionTestData.Envelope(), effect, default,
            requiresOwnerReconciliation: effect == AgentToolEffectState.Unknown);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.PauseForBasisChangeAsync(lease.PlanningWork!, Binding(waiting), default));
        var unchanged = await fixture.ReadAsync();
        Assert.Equal(1, unchanged.ToolAdmission!.PlanningDecision!.CurrentGeneration);
        Assert.Equal(effect, AgentToolJournalTransitions.RequireProposal(unchanged.ToolAdmission, Binding(waiting)).EffectState);
    }

    private static async Task<ExecutionRunRecord> WaitForApprovalAsync(AgentPlanningLiveOwnerFixture fixture, TimeSpan? spent = null) {
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        await fixture.RespondAsync(work, dispatch, publish: true);
        fixture.Clock.Advance(spent ?? TimeSpan.FromSeconds(1));
        var call = AgentPlanningLiveOwnerFixture.Publication();
        return await fixture.Owner.PauseForApprovalAsync(work, new(Guid.NewGuid().ToString("D"), call.CallId,
            call.Payload.ToolName, "function", "Review original task plan.", call.Payload.ArgumentsJson), default);
    }

    private static AgentToolApprovalBinding Binding(ExecutionRunRecord run)
        => run.ToolAdmission!.PlanningDecision!.Current.Steps[^1].Publications.Single().Proposal;

    private static AgentSavedEffectContinuationRequest Request(ExecutionRunRecord run)
        => new(run.Id, run.ChatSessionId, run.Revision, run.ToolAdmission!.Revision, Binding(run),
            run.PendingApprovals.Select(approval => new PendingToolApprovalDecision(approval.ApprovalId, true) {
                ToolAdmission = approval.ToolAdmission
            }).ToArray());

    private static Task<AgentToolRunLease> ResumeAsync(AgentPlanningLiveOwnerFixture fixture, ExecutionRunRecord run)
        => fixture.Owner.ResumeSavedEffectAsync(Request(run), AgentPlanningDecisionExpectation.From(run), AllowRead, default);

    private static async Task<AgentToolInvocationClaim> ClaimAsync(AgentPlanningLiveOwnerFixture fixture,
        AgentToolRunLease lease, ExecutionRunRecord run) {
        await fixture.Journal.ObserveSavedEffectReceiptAsync(lease, new AbsentReceipt(), default);
        return await fixture.Journal.ClaimSavedInvocationAsync(lease, Binding(run),
            AgentToolJournalTransitions.RequireProposal(run.ToolAdmission!, Binding(run)).Payload, default);
    }

    private static async Task<IAgentPlanningPublicationSnapshotLease> SnapshotAsync(AgentPlanningLiveOwnerFixture fixture) {
        var run = await fixture.ReadAsync();
        return await fixture.Files.Store.AcquirePlanningAuthoritySnapshotAsync(new(fixture.Files.StorageScope,
            run.ToolAdmission!.Session, run.Revision, run.ToolAdmission.Revision));
    }

    private static async Task<ExecutionRunRecord> RecordNoEffectPauseAsync(AgentPlanningLiveOwnerFixture fixture, ExecutionRunRecord waiting) {
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        var claim = await ClaimAsync(fixture, lease, waiting);
        using var claimed = claim.Bind();
        await fixture.Journal.CompleteInvocationAsync(claim, AgentPlanningDecisionTestData.Envelope("{\"basisChanged\":true}"),
            AgentToolEffectState.NotCommitted, default);
        await fixture.Journal.CheckpointSavedEffectAsync(lease, default);
        return await fixture.Owner.PauseForBasisChangeAsync(lease.PlanningWork!, Binding(waiting), default);
    }

    private static ValueTask<bool> AllowRead(AgentToolSessionObservation observation, CancellationToken token) => new(true);

    private sealed class AbsentReceipt : IAgentToolReceiptReconciliationProvider {
        public bool Supports(string toolName) => toolName == AgentPlanningLiveOwnerFixture.Publication().Payload.ToolName;
        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            await claim.RequireAsync(cancellationToken);
            return AgentToolReceiptObservation.NotObserved;
        }
    }

    public enum InvalidAction { LegacyAdmission, Decision, Generation, Binding }
}
