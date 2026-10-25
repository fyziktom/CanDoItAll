using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel;
using static CanDoItAll.Tests.Integration.Runtime.AgentInheritedPlanningFixture;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed class AgentInheritedPlanningOwnerTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Actual_background_admission_and_successive_provider_steps_keep_one_original_basis(ReadBasisMode parentMode) {
        await using var fixture = await CreateAsync(parentMode);
        var original = fixture.Original.ToolAdmission!;
        Assert.Null(fixture.Original.ChatSessionId);
        Assert.Null(original.OriginalInput);
        Assert.Null(original.PlanningDecision!.Provenance);
        Assert.Equal(parentMode, original.PlanningDecision.InheritedOrigin!.ParentPolicy.ReadBasisMode);
        Assert.Equal(original.Session.Profile, original.PlanningDecision.InheritedOrigin.Profile);
        await using var lease = await fixture.StartAsync();
        using var bound = lease.Bind();
        var work = Assert.IsType<AgentPlanningLiveWork>(lease.PlanningWork);
        await fixture.InheritAsync(work);
        var entered = await fixture.EnterAsync(work);
        Assert.Null((await fixture.RespondAsync(work, entered)).Refusal);
        fixture.Clock.Advance(TimeSpan.FromSeconds(3));
        var next = await fixture.Owner.AdvanceStepAsync(work, default);
        await fixture.Owner.InheritBasisAsync(next, (_, _) => throw new InvalidOperationException("Original material must be reused."), default);
        var second = await fixture.EnterAsync(next);
        await fixture.RespondAsync(next, second);
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        await fixture.Owner.SettleTerminalAsync(next, AgentPlanningGenerationState.Completed, default);
        var reopened = await fixture.ReadAsync();
        var decision = reopened.ToolAdmission!.PlanningDecision!;
        Assert.Equal(original.PlanningDecision.InheritedOrigin, decision.InheritedOrigin);
        Assert.Equal(fixture.Basis, Assert.Single(decision.Current.Bases));
        Assert.Equal(2, decision.Current.Steps.Length);
        Assert.All(decision.Current.Steps, step => Assert.Equal(fixture.Basis.Id, step.BasisId));
        Assert.Equal(TimeSpan.FromSeconds(5).Ticks, decision.Work.ClosedWorkTicks);
        Assert.Null(decision.Work.Open);
        Assert.Equal(RunOutcome.Succeeded, reopened.Outcome);
    }

    [Fact]
    public async Task Existing_execution_is_looked_up_before_origin_binding_or_replacement_admission() {
        await using var fixture = await CreateAsync();
        var factory = new AgentInheritedPlanningAdmission(fixture.NewJournal());
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => factory.CreateAsync(fixture.Original with { ToolAdmission = null },
            "replacement", AgentPlanningOwner.ProjectTaskPlan, fixture.Policy.ParentBinding,
            (_, _) => throw new InvalidOperationException("Binding must not run.")));
        var retained = await fixture.ReadAsync();
        Assert.Equal(fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin, retained.ToolAdmission!.PlanningDecision!.InheritedOrigin);
        Assert.Equal(fixture.Original.Revision, retained.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Parent_denial_or_changed_claim_prevents_opening_work(bool changeClaim) {
        await using var fixture = await CreateAsync();
        if (changeClaim) {
            fixture.Policy.Fingerprint = new(new string('a', 64));
        } else {
            fixture.Policy.DispatchAllowed = false;
        }
        if (changeClaim) {
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.StartAsync());
        } else {
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.StartAsync());
        }
        var retained = await fixture.ReadAsync();
        Assert.Equal(fixture.Original.Revision, retained.Revision);
        Assert.Empty(retained.ToolAdmission!.PlanningDecision!.Current.Steps);
        Assert.Null(retained.ToolAdmission.PlanningDecision.Work.Open);
    }

    [Fact]
    public async Task Denied_current_reader_cannot_open_inherited_work() {
        await using var fixture = await CreateAsync();
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Owner.StartInheritedAsync(
            AgentPlanningDecisionExpectation.From(fixture.Original), (_, _) => ValueTask.FromResult(false), default));
        Assert.Null((await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work.Open);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Material_substitution_never_attaches_a_segment_or_enters_provider(int field) {
        await using var fixture = await CreateAsync();
        await using var lease = await fixture.StartAsync();
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        var basis = field switch {
            0 => fixture.Basis with { CapturedAtUtc = fixture.Clock.Now },
            1 => fixture.Basis with { Id = Guid.NewGuid() },
            _ => fixture.Basis with { Value = AgentToolAdmissionJournalFixture.Envelope("{\"changed\":true}") }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Owner.InheritBasisAsync(work, (_, _) => Task.FromResult(basis), default));
        var retained = (await fixture.ReadAsync()).ToolAdmission!;
        Assert.Empty(retained.Segments);
        Assert.Empty(retained.PlanningDecision!.Current.Bases);
        Assert.True(retained.ProviderDispatches.IsDefaultOrEmpty);
        await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Failed, default);
    }

    [Fact]
    public async Task Interactive_entry_capture_and_refresh_cannot_promote_inherited_authority() {
        await using var fixture = await CreateAsync();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Owner.StartAsync(
            AgentPlanningDecisionExpectation.From(fixture.Original), AllowRead, default));
        await using var lease = await fixture.StartAsync();
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.StartStepAsync(lease,
            AgentPlanningDecisionExpectation.From(fixture.Original), default));
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.CaptureBasisAsync(work,
            (_, _) => throw new InvalidOperationException("An inherited owner cannot recapture material."), default));
        await fixture.InheritAsync(work);
        await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Failed, default);
        var before = await fixture.ReadAsync();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Owner.RefreshAsync(
            AgentPlanningDecisionExpectation.From(before), false, AllowRead, default));
        Assert.Equal(before.Revision, (await fixture.ReadAsync()).Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Parent_revocation_after_material_read_blocks_provider_entry(bool changeClaim) {
        await using var fixture = await CreateAsync();
        await using var lease = await fixture.StartAsync();
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        await fixture.InheritAsync(work);
        if (changeClaim) {
            fixture.Policy.Fingerprint = new(new string('a', 64));
        } else {
            fixture.Policy.DispatchAllowed = false;
        }
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.EnterAsync(work));
        Assert.True((await fixture.ReadAsync()).ToolAdmission!.ProviderDispatches.IsDefaultOrEmpty);
        await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Cancelled, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revocation_keeps_actual_response_and_work_settlement_without_admitting_another_step(bool revokeRead) {
        await using var fixture = await CreateAsync();
        await using var lease = await fixture.StartAsync();
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        await fixture.InheritAsync(work);
        var dispatch = await fixture.EnterAsync(work);
        fixture.Policy.DispatchAllowed = false;
        fixture.Policy.ReadAllowed = !revokeRead;
        fixture.Clock.Advance(TimeSpan.FromSeconds(4));
        await fixture.RespondAsync(work, dispatch);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Owner.AdvanceStepAsync(work, default));
        await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Cancelled, default);
        var retained = await fixture.ReadAsync();
        Assert.Equal(AgentToolProviderDispatchState.ResponseAdmitted, retained.ToolAdmission!.ProviderDispatches.Single().State);
        Assert.Equal(TimeSpan.FromSeconds(4).Ticks, retained.ToolAdmission.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Equal(fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin, retained.ToolAdmission.PlanningDecision.InheritedOrigin);
        Assert.Null(retained.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Single(retained.ToolAdmission.PlanningDecision.Current.Steps);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inherited_capture_age_and_finite_grant_are_checked_before_provider_entry(bool expireBasis) {
        await using var fixture = await CreateAsync(basisAge: expireBasis ? TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1) : null);
        await using var lease = await fixture.StartAsync();
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        await fixture.InheritAsync(work);
        fixture.Clock.Advance(TimeSpan.FromSeconds(expireBasis ? 1 : 30));
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.EnterAsync(work));
        Assert.Equal(expireBasis ? AgentPlanningAdmissionRefusal.BasisExpired : AgentPlanningAdmissionRefusal.WorkBudgetExhausted, failure.Reason);
        Assert.True((await fixture.ReadAsync()).ToolAdmission!.ProviderDispatches.IsDefaultOrEmpty);
        await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Failed, default);
    }

    [Fact]
    public async Task Lost_start_acknowledgement_and_restart_keep_original_work_and_grant() {
        await using var fixture = await CreateAsync();
        fixture.Fault = ExistingRunDetailCommitStage.RunPersisted;
        AgentToolAdmissionJournalFixture.RequireScriptedFault(await Assert.ThrowsAnyAsync<Exception>(() => fixture.StartAsync()), "inherited-planning-ack");
        var retained = await fixture.ReadAsync();
        var open = retained.ToolAdmission!.PlanningDecision!.Work.Open;
        Assert.NotNull(open);
        var restarted = new AgentPlanningDecisionOwner(fixture.NewJournal());
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => restarted.StartInheritedAsync(
            AgentPlanningDecisionExpectation.From(retained), AllowRead, default));
        Assert.Equal(AgentPlanningAdmissionRefusal.InterruptedWorkAccountingUnavailable, failure.Reason);
        var after = await fixture.ReadAsync();
        Assert.Equal(open, after.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(retained.Revision, after.Revision);
        Assert.Equal(fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin, after.ToolAdmission.PlanningDecision.InheritedOrigin);
    }

    [Fact]
    public async Task Lost_material_acknowledgement_reopens_exact_bytes_and_does_not_recapture() {
        await using var fixture = await CreateAsync();
        await using var lease = await fixture.StartAsync();
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        fixture.Fault = ExistingRunDetailCommitStage.RunPersisted;
        AgentToolAdmissionJournalFixture.RequireScriptedFault(await Assert.ThrowsAnyAsync<Exception>(() => fixture.InheritAsync(work)), "inherited-planning-ack");
        var retained = (await fixture.ReadAsync()).ToolAdmission!;
        Assert.Equal(fixture.Basis, Assert.Single(retained.PlanningDecision!.Current.Bases));
        Assert.Single(retained.Segments);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.InheritBasisAsync(work,
            (_, _) => throw new InvalidOperationException("Committed material cannot be recaptured."), default));
        var observation = await fixture.Owner.ObserveStepAsync(work, default);
        Assert.Equal(fixture.Basis, observation.Basis);
        Assert.Equal(fixture.Files.StorageScope, observation.Scope);
        await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Failed, default);
    }
}
